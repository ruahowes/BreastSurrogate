using System;
using System.IO;

namespace BreastSurrogate.Batch
{
    internal static class InteractiveInput
    {
        public static bool ShouldPrompt(
            string[] arguments,
            bool isInputRedirected,
            bool isUserInteractive)
        {
            return isUserInteractive
                && !isInputRedirected
                && (arguments == null || arguments.Length == 0);
        }

        public static string[] PromptForPaths(TextReader reader, TextWriter writer)
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            writer.WriteLine();
            writer.WriteLine(BatchCommandLine.EsapiStartupCheckUsage);
            writer.WriteLine(
                "Enter a file path, or a directory containing the conventional file name.");
            writer.Write("Patient-list CSV path (or T to test ESAPI startup): ");
            string patientPath = reader.ReadLine();
            if (string.Equals(patientPath, "T", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "--check-esapi" };
            }

            writer.Write("JSON configuration path: ");
            string configurationPath = reader.ReadLine();
            return new[] { patientPath ?? string.Empty, configurationPath ?? string.Empty };
        }
    }
}
