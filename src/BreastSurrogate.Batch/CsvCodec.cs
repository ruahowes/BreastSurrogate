using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BreastSurrogate.Batch
{
    internal sealed class CsvRecord
    {
        public CsvRecord(int lineNumber, IList<string> fields)
        {
            LineNumber = lineNumber;
            Fields = fields;
        }

        public int LineNumber { get; private set; }
        public IList<string> Fields { get; private set; }
    }

    internal static class CsvCodec
    {
        public static bool TryRead(
            TextReader reader,
            out IList<CsvRecord> records,
            out string error)
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            var parsed = new List<CsvRecord>();
            var fields = new List<string>();
            var field = new StringBuilder();
            int lineNumber = 1;
            int recordLineNumber = 1;
            bool inQuotes = false;
            bool quotedFieldClosed = false;
            bool fieldStarted = false;

            while (true)
            {
                int read = reader.Read();
                if (read < 0)
                {
                    if (inQuotes)
                    {
                        records = null;
                        error = "CSV contains an unterminated quoted field starting on line "
                            + recordLineNumber + ".";
                        return false;
                    }

                    if (fieldStarted || quotedFieldClosed || field.Length > 0 || fields.Count > 0)
                    {
                        fields.Add(field.ToString());
                        parsed.Add(new CsvRecord(recordLineNumber, fields));
                    }

                    records = parsed;
                    error = null;
                    return true;
                }

                char character = (char)read;
                if (inQuotes)
                {
                    if (character == '"')
                    {
                        if (reader.Peek() == '"')
                        {
                            reader.Read();
                            field.Append('"');
                        }
                        else
                        {
                            inQuotes = false;
                            quotedFieldClosed = true;
                        }
                    }
                    else
                    {
                        field.Append(character);
                        if (character == '\n')
                        {
                            lineNumber++;
                        }
                    }

                    continue;
                }

                if (quotedFieldClosed && character != ',' && character != '\r' && character != '\n')
                {
                    records = null;
                    error = "CSV has unexpected text after a closing quote on line "
                        + lineNumber + ".";
                    return false;
                }

                if (character == '"')
                {
                    if (fieldStarted || field.Length > 0)
                    {
                        records = null;
                        error = "CSV has a quote inside an unquoted field on line "
                            + lineNumber + ".";
                        return false;
                    }

                    inQuotes = true;
                    fieldStarted = true;
                }
                else if (character == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    quotedFieldClosed = false;
                }
                else if (character == '\r' || character == '\n')
                {
                    if (character == '\r' && reader.Peek() == '\n')
                    {
                        reader.Read();
                    }

                    fields.Add(field.ToString());
                    parsed.Add(new CsvRecord(recordLineNumber, fields));
                    fields = new List<string>();
                    field.Clear();
                    fieldStarted = false;
                    quotedFieldClosed = false;
                    lineNumber++;
                    recordLineNumber = lineNumber;
                }
                else
                {
                    field.Append(character);
                    fieldStarted = true;
                }
            }
        }

        public static void WriteRecord(TextWriter writer, IEnumerable<string> fields)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            bool first = true;
            foreach (string field in fields)
            {
                if (!first)
                {
                    writer.Write(',');
                }

                WriteField(writer, field ?? string.Empty);
                first = false;
            }

            writer.WriteLine();
        }

        private static void WriteField(TextWriter writer, string field)
        {
            bool requiresQuotes = field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
                || (field.Length > 0
                    && (char.IsWhiteSpace(field[0])
                        || char.IsWhiteSpace(field[field.Length - 1])));
            if (!requiresQuotes)
            {
                writer.Write(field);
                return;
            }

            writer.Write('"');
            writer.Write(field.Replace("\"", "\"\""));
            writer.Write('"');
        }
    }
}
