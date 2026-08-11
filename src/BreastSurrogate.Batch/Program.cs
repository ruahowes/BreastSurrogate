using System;
using System.Collections.Generic;
using System.IO;
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
                Console.Error.WriteLine("ESAPI application startup or shutdown failed.");
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
                Console.WriteLine(
                    "Phase 12D input scaffold only: no patient was opened and no ARIA data was modified.");
            }

            Console.WriteLine("ESAPI application disposed successfully.");
            return SuccessExitCode;
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
