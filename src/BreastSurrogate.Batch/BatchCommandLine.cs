using System;
using System.IO;

namespace BreastSurrogate.Batch
{
    public sealed class BatchCommandLineOptions
    {
        public BatchCommandLineOptions(string patientListPath, string configurationPath)
        {
            PatientListPath = patientListPath;
            ConfigurationPath = configurationPath;
        }

        public string PatientListPath { get; private set; }

        public string ConfigurationPath { get; private set; }
    }

    public static class BatchCommandLine
    {
        public const string Usage = "Usage: BreastSurrogate.Batch.exe patients.csv config.json";
        public const string EsapiStartupCheckUsage =
            "ESAPI startup check: BreastSurrogate.Batch.exe --check-esapi";

        public static bool IsEsapiStartupCheck(string[] arguments)
        {
            return arguments != null
                && arguments.Length == 1
                && string.Equals(
                    arguments[0],
                    "--check-esapi",
                    StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParse(
            string[] arguments,
            out BatchCommandLineOptions options,
            out string error)
        {
            return TryParse(arguments, File.Exists, out options, out error);
        }

        internal static bool TryParse(
            string[] arguments,
            Func<string, bool> fileExists,
            out BatchCommandLineOptions options,
            out string error)
        {
            return TryParse(
                arguments,
                fileExists,
                Directory.Exists,
                out options,
                out error);
        }

        internal static bool TryParse(
            string[] arguments,
            Func<string, bool> fileExists,
            Func<string, bool> directoryExists,
            out BatchCommandLineOptions options,
            out string error)
        {
            if (fileExists == null)
            {
                throw new ArgumentNullException("fileExists");
            }

            if (directoryExists == null)
            {
                throw new ArgumentNullException("directoryExists");
            }

            options = null;
            if (arguments == null || arguments.Length != 2)
            {
                error = "Expected exactly two input paths. " + Usage;
                return false;
            }

            string patientListPath;
            if (!TryNormalizePath(arguments[0], out patientListPath, out error))
            {
                return false;
            }

            string configurationPath;
            if (!TryNormalizePath(arguments[1], out configurationPath, out error))
            {
                return false;
            }

            patientListPath = ResolveFileOrDirectory(
                patientListPath,
                "patients.csv",
                directoryExists);
            configurationPath = ResolveFileOrDirectory(
                configurationPath,
                "config.json",
                directoryExists);

            if (!fileExists(patientListPath))
            {
                error = "Patient-list CSV was not found: " + patientListPath;
                return false;
            }

            if (!fileExists(configurationPath))
            {
                error = "JSON configuration was not found: " + configurationPath;
                return false;
            }

            options = new BatchCommandLineOptions(patientListPath, configurationPath);
            error = null;
            return true;
        }

        private static bool TryNormalizePath(
            string path,
            out string normalizedPath,
            out string error)
        {
            normalizedPath = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Input paths cannot be empty. " + Usage;
                return false;
            }

            try
            {
                string trimmedPath = path.Trim();
                if (trimmedPath.Length >= 2
                    && trimmedPath[0] == '"'
                    && trimmedPath[trimmedPath.Length - 1] == '"')
                {
                    trimmedPath = trimmedPath.Substring(1, trimmedPath.Length - 2);
                }

                normalizedPath = Path.GetFullPath(trimmedPath);
                error = null;
                return true;
            }
            catch (ArgumentException)
            {
                error = "An input path is invalid. " + Usage;
                return false;
            }
            catch (NotSupportedException)
            {
                error = "An input path uses an unsupported format. " + Usage;
                return false;
            }
            catch (PathTooLongException)
            {
                error = "An input path is too long. " + Usage;
                return false;
            }
        }

        private static string ResolveFileOrDirectory(
            string path,
            string conventionalFileName,
            Func<string, bool> directoryExists)
        {
            return directoryExists(path)
                ? Path.Combine(path, conventionalFileName)
                : path;
        }
    }
}
