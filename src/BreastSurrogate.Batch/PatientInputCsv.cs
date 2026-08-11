using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BreastSurrogate.Batch
{
    public sealed class PatientInputRow
    {
        internal PatientInputRow(
            int lineNumber,
            string patientId,
            string planningCourseId,
            string physicsCourseId,
            string physicsPlanId)
        {
            LineNumber = lineNumber;
            PatientId = patientId;
            PlanningCourseId = planningCourseId;
            PhysicsCourseId = physicsCourseId;
            PhysicsPlanId = physicsPlanId;
        }

        public int LineNumber { get; private set; }
        public string PatientId { get; private set; }
        public string PlanningCourseId { get; private set; }
        public string PhysicsCourseId { get; private set; }
        public string PhysicsPlanId { get; private set; }
    }

    public static class PatientInputCsv
    {
        private static readonly string[] KnownHeaders =
        {
            "PatientId",
            "PlanningCourseId",
            "PhysicsCourseId",
            "PhysicsPlanId"
        };

        public static bool TryLoad(
            string path,
            out IList<PatientInputRow> rows,
            out string error)
        {
            try
            {
                using (var reader = new StreamReader(path, true))
                {
                    return TryRead(reader, out rows, out error);
                }
            }
            catch (Exception exception)
            {
                rows = null;
                error = "Could not read patient-list CSV: " + exception.Message;
                return false;
            }
        }

        internal static bool TryRead(
            TextReader reader,
            out IList<PatientInputRow> rows,
            out string error)
        {
            IList<CsvRecord> records;
            if (!CsvCodec.TryRead(reader, out records, out error))
            {
                rows = null;
                return false;
            }

            records = records.Where(record => !IsBlank(record)).ToList();
            if (records.Count == 0)
            {
                rows = null;
                error = "Patient-list CSV is empty.";
                return false;
            }

            Dictionary<string, int> columns;
            if (!TryMapHeaders(records[0], out columns, out error))
            {
                rows = null;
                return false;
            }

            var parsed = new List<PatientInputRow>();
            var exactRows = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 1; index < records.Count; index++)
            {
                CsvRecord record = records[index];
                if (record.Fields.Count != records[0].Fields.Count)
                {
                    rows = null;
                    error = "Patient-list CSV line " + record.LineNumber
                        + " has " + record.Fields.Count
                        + " fields; expected " + records[0].Fields.Count + ".";
                    return false;
                }

                string patientId = Read(record, columns, "PatientId");
                if (string.IsNullOrWhiteSpace(patientId))
                {
                    rows = null;
                    error = "Patient-list CSV line " + record.LineNumber
                        + " has an empty PatientId.";
                    return false;
                }

                string planningCourseId = Read(record, columns, "PlanningCourseId");
                string physicsCourseId = Read(record, columns, "PhysicsCourseId");
                string physicsPlanId = Read(record, columns, "PhysicsPlanId");
                string identity = BuildExactRowIdentity(
                    patientId,
                    planningCourseId,
                    physicsCourseId,
                    physicsPlanId);
                if (!exactRows.Add(identity))
                {
                    rows = null;
                    error = "Patient-list CSV line " + record.LineNumber
                        + " exactly duplicates an earlier input row.";
                    return false;
                }

                parsed.Add(new PatientInputRow(
                    record.LineNumber,
                    patientId,
                    EmptyToNull(planningCourseId),
                    EmptyToNull(physicsCourseId),
                    EmptyToNull(physicsPlanId)));
            }

            if (parsed.Count == 0)
            {
                rows = null;
                error = "Patient-list CSV contains a header but no patient rows.";
                return false;
            }

            rows = parsed.AsReadOnly();
            error = null;
            return true;
        }

        private static bool TryMapHeaders(
            CsvRecord header,
            out Dictionary<string, int> columns,
            out string error)
        {
            columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < header.Fields.Count; index++)
            {
                string name = header.Fields[index].Trim().TrimStart('\uFEFF');
                if (!KnownHeaders.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    error = "Patient-list CSV has an unknown column: " + name;
                    return false;
                }

                if (columns.ContainsKey(name))
                {
                    error = "Patient-list CSV has a duplicate column: " + name;
                    return false;
                }

                columns.Add(name, index);
            }

            if (!columns.ContainsKey("PatientId"))
            {
                error = "Patient-list CSV requires a PatientId column.";
                return false;
            }

            error = null;
            return true;
        }

        private static string Read(
            CsvRecord record,
            IDictionary<string, int> columns,
            string name)
        {
            int index;
            return columns.TryGetValue(name, out index)
                ? record.Fields[index]
                : string.Empty;
        }

        private static bool IsBlank(CsvRecord record)
        {
            return record.Fields.All(string.IsNullOrWhiteSpace);
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string BuildExactRowIdentity(params string[] values)
        {
            return string.Concat(values.Select(
                value => value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ":" + value));
        }
    }
}
