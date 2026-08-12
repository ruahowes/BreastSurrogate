using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using VMS.TPS.Common.Model.API;

namespace BreastSurrogate.Batch
{
    internal static class Program
    {
        private const int SuccessExitCode = 0;
        private const int InvalidInputExitCode = 2;
        private const int ApplicationStartupFailureExitCode = 3;

        [STAThread]
        private static int Main(string[] arguments)
        {
            bool pauseBeforeExit = InteractiveInput.ShouldPrompt(
                arguments,
                Console.IsInputRedirected,
                Environment.UserInteractive);
            if (pauseBeforeExit)
            {
                arguments = InteractiveInput.PromptForPaths(Console.In, Console.Out);
            }

            if (BatchCommandLine.IsEsapiStartupCheck(arguments))
            {
                return RunEsapiApplicationSafely(
                    null,
                    null,
                    null,
                    pauseBeforeExit);
            }

            BatchCommandLineOptions options;
            string error;
            if (!BatchCommandLine.TryParse(arguments, out options, out error))
            {
                Console.Error.WriteLine(error);
                PauseBeforeExit(pauseBeforeExit);
                return InvalidInputExitCode;
            }

            BatchConfiguration configuration;
            if (!BatchConfigurationLoader.TryLoad(
                options.ConfigurationPath,
                out configuration,
                out error))
            {
                Console.Error.WriteLine(error);
                PauseBeforeExit(pauseBeforeExit);
                return InvalidInputExitCode;
            }

            IList<PatientInputRow> patientRows;
            if (!PatientInputCsv.TryLoad(
                options.PatientListPath,
                out patientRows,
                out error))
            {
                Console.Error.WriteLine(error);
                PauseBeforeExit(pauseBeforeExit);
                return InvalidInputExitCode;
            }

            return RunEsapiApplicationSafely(
                options,
                configuration,
                patientRows,
                pauseBeforeExit);
        }

        private static int RunEsapiApplicationSafely(
            BatchCommandLineOptions options,
            BatchConfiguration configuration,
            IList<PatientInputRow> patientRows,
            bool pauseBeforeExit)
        {
            int exitCode;
            try
            {
                exitCode = RunEsapiApplication(
                    options,
                    configuration,
                    patientRows);
            }
            catch (FileNotFoundException exception)
            {
                ReportAssemblyLoadFailure(exception);
                exitCode = ApplicationStartupFailureExitCode;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Fatal ESAPI startup, batch output, or shutdown failure.");
                Console.Error.WriteLine(exception.GetType().FullName + ": " + exception.Message);
                exitCode = ApplicationStartupFailureExitCode;
            }

            PauseBeforeExit(pauseBeforeExit);
            return exitCode;
        }

        private static int RunEsapiApplication(
            BatchCommandLineOptions options,
            BatchConfiguration configuration,
            IList<PatientInputRow> patientRows)
        {
            Console.WriteLine("BreastSurrogate standalone batch host");
            if (options == null)
            {
                Console.WriteLine("Mode: ESAPI application startup check");
            }
            else
            {
                Console.WriteLine("Patient list: " + options.PatientListPath);
                Console.WriteLine("Configuration: " + options.ConfigurationPath);
                Console.WriteLine("Validated patient rows: " + patientRows.Count);
                Console.WriteLine("Configured DVH metrics: " + configuration.Metrics.Count);
                Console.WriteLine("Log directory: " + configuration.LogDirectory);
                Console.WriteLine("Output directory: " + configuration.OutputDirectory);
            }

            Console.WriteLine("Starting read-only ESAPI application...");
            using (Application application = Application.CreateApplication())
            {
                Console.WriteLine("ESAPI application initialized successfully.");
                if (options == null)
                {
                    Console.WriteLine(
                        "Startup check only: no patient was opened and no ARIA data was modified.");
                }
                else
                {
                    RunBatch(application, configuration, patientRows);
                }
            }

            Console.WriteLine("ESAPI application disposed successfully.");
            return SuccessExitCode;
        }

        private static void RunBatch(
            Application application,
            BatchConfiguration configuration,
            IList<PatientInputRow> patientRows)
        {
            string runId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string outputPath = Path.Combine(
                configuration.OutputDirectory,
                "BreastSurrogateAudit_" + runId + ".csv");
            string batchLogPath = Path.Combine(
                configuration.LogDirectory,
                "BreastSurrogateAudit_" + runId + ".log");
            string applicationVersion = Assembly.GetExecutingAssembly()
                .GetName()
                .Version
                .ToString();

            Console.WriteLine("Output CSV: " + outputPath);
            Console.WriteLine("Batch log: " + batchLogPath);
            var utf8 = new UTF8Encoding(false);
            using (var outputStream = new FileStream(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            using (var outputWriter = new StreamWriter(outputStream, utf8))
            using (var logStream = new FileStream(
                batchLogPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            using (var batchLog = new StreamWriter(logStream, utf8))
            {
                var csv = new BatchOutputCsvWriter(
                    outputWriter,
                    BatchOutputSchema.Create(configuration));
                var progress = new ConsoleProgressReporter(
                    Console.Out,
                    !Console.IsOutputRedirected);
                var runner = new BatchAuditRunner();
                BatchRunSummary summary = runner.Run(
                    patientRows,
                    configuration,
                    applicationVersion,
                    new EsapiPatientAuditSource(application),
                    csv,
                    batchLog,
                    (index, input, lines) => WritePatientLog(
                        configuration.LogDirectory,
                        runId,
                        index,
                        input,
                        lines,
                        utf8),
                    progress);

                Console.WriteLine("Batch completed.");
                Console.WriteLine("Rows: " + summary.TotalRows);
                Console.WriteLine("Fully available: " + summary.SuccessfulRows);
                Console.WriteLine("With unavailable values: " + summary.RowsWithFailures);
            }
        }

        private static void WritePatientLog(
            string directory,
            string runId,
            int index,
            PatientInputRow input,
            IList<string> lines,
            Encoding encoding)
        {
            string path = Path.Combine(
                directory,
                "BreastSurrogateAudit_"
                    + runId
                    + "_"
                    + (index + 1).ToString("0000", CultureInfo.InvariantCulture)
                    + "_"
                    + SanitizeFileName(input.PatientId)
                    + ".log");
            File.WriteAllLines(path, lines, encoding);
        }

        internal static string SanitizeFileName(string value)
        {
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            var safe = new StringBuilder();
            foreach (char character in value ?? string.Empty)
            {
                safe.Append(invalid.Contains(character) ? '_' : character);
            }

            string result = safe.ToString().Trim().TrimEnd('.');
            return result.Length == 0 ? "patient" : result;
        }

        internal static bool ShouldPauseAfterInvalidInput(
            string[] arguments,
            bool isInputRedirected,
            bool isUserInteractive)
        {
            return InteractiveInput.ShouldPrompt(
                arguments,
                isInputRedirected,
                isUserInteractive);
        }

        private static void PauseBeforeExit(bool pauseBeforeExit)
        {
            if (!pauseBeforeExit)
            {
                return;
            }

            Console.WriteLine();
            Console.Write("Press Enter to close...");
            Console.ReadLine();
        }

        private static void ReportAssemblyLoadFailure(FileNotFoundException exception)
        {
            Console.Error.WriteLine("ESAPI application startup failed.");
            Console.Error.WriteLine(exception.GetType().FullName + ": " + exception.Message);
        }
    }
}
