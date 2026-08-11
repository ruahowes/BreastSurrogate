using System;
using System.IO;
using System.Text;

namespace BreastSurrogate.Batch
{
    /// <summary>
    /// Sequential console progress display. Inspired by the ConsoleUtility.cs
    /// reference supplied in docs, but uses carriage-return line replacement
    /// and a redirected-output fallback rather than a fixed backspace count.
    /// </summary>
    public sealed class ConsoleProgressReporter
    {
        private const int DefaultBarWidth = 30;

        private readonly TextWriter _writer;
        private readonly bool _supportsInPlaceUpdate;
        private readonly int _barWidth;
        private int _previousLineLength;
        private int _lastRedirectedCompleted = -1;

        public ConsoleProgressReporter(TextWriter writer, bool supportsInPlaceUpdate)
            : this(writer, supportsInPlaceUpdate, DefaultBarWidth)
        {
        }

        public ConsoleProgressReporter(
            TextWriter writer,
            bool supportsInPlaceUpdate,
            int barWidth)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            if (barWidth <= 0)
            {
                throw new ArgumentOutOfRangeException("barWidth");
            }

            _writer = writer;
            _supportsInPlaceUpdate = supportsInPlaceUpdate;
            _barWidth = barWidth;
        }

        public void Report(int completed, int total, string message)
        {
            if (total <= 0)
            {
                throw new ArgumentOutOfRangeException("total", "Total must be positive.");
            }

            if (completed < 0 || completed > total)
            {
                throw new ArgumentOutOfRangeException(
                    "completed",
                    "Completed must be between zero and total inclusive.");
            }

            if (!_supportsInPlaceUpdate && completed == _lastRedirectedCompleted)
            {
                return;
            }

            string line = FormatLine(completed, total, message);
            if (_supportsInPlaceUpdate)
            {
                int padding = Math.Max(0, _previousLineLength - line.Length);
                _writer.Write('\r');
                _writer.Write(line);
                if (padding > 0)
                {
                    _writer.Write(new string(' ', padding));
                }

                if (completed == total)
                {
                    _writer.WriteLine();
                    _previousLineLength = 0;
                }
                else
                {
                    _previousLineLength = line.Length;
                }
            }
            else
            {
                _writer.WriteLine(line);
                _lastRedirectedCompleted = completed;
            }

            _writer.Flush();
        }

        internal string FormatLine(int completed, int total, string message)
        {
            int percentage = (int)Math.Round(
                100.0 * completed / total,
                MidpointRounding.AwayFromZero);
            int filled = (int)Math.Round(
                (double)_barWidth * completed / total,
                MidpointRounding.AwayFromZero);
            string safeMessage = SanitizeMessage(message);
            var line = new StringBuilder();
            line.Append('[');
            line.Append(new string('#', filled));
            line.Append(new string(' ', _barWidth - filled));
            line.Append("] ");
            line.Append(completed);
            line.Append('/');
            line.Append(total);
            line.Append(' ');
            line.Append(percentage.ToString("000"));
            line.Append('%');
            if (safeMessage.Length > 0)
            {
                line.Append(' ');
                line.Append(safeMessage);
            }

            return line.ToString();
        }

        private static string SanitizeMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}
