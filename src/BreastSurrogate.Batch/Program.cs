using System;
using VMS.TPS.Common.Model.API;

namespace BreastSurrogate.Batch
{
    internal static class Program
    {
        private const int SuccessExitCode = 0;
        private const int InvalidInputExitCode = 2;
        private const int ApplicationStartupFailureExitCode = 3;
        private const int ApplicationShutdownFailureExitCode = 4;

        [STAThread]
        private static int Main(string[] arguments)
        {
            if (BatchCommandLine.IsEsapiStartupCheck(arguments))
            {
                return RunEsapiApplication(null, false);
            }

            BatchCommandLineOptions options;
            string error;
            if (!BatchCommandLine.TryParse(arguments, out options, out error))
            {
                Console.Error.WriteLine(error);
                if (OfferEsapiCheckAfterNoArgumentLaunch(arguments))
                {
                    return RunEsapiApplication(null, true);
                }

                return InvalidInputExitCode;
            }

            return RunEsapiApplication(options, false);
        }

        private static int RunEsapiApplication(
            BatchCommandLineOptions options,
            bool pauseBeforeExit)
        {
            Application application = null;
            int exitCode;
            try
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
                }

                Console.WriteLine("Starting read-only ESAPI application...");

                application = Application.CreateApplication();

                Console.WriteLine("ESAPI application initialized successfully.");
                Console.WriteLine(
                    "Phase 12C scaffold only: no patient was opened and no ARIA data was modified.");
                exitCode = SuccessExitCode;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("ESAPI application startup failed.");
                Console.Error.WriteLine(exception.GetType().FullName + ": " + exception.Message);
                exitCode = ApplicationStartupFailureExitCode;
            }
            finally
            {
                if (application != null)
                {
                    try
                    {
                        application.Dispose();
                        Console.WriteLine("ESAPI application disposed successfully.");
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLine("ESAPI application disposal failed.");
                        Console.Error.WriteLine(
                            exception.GetType().FullName + ": " + exception.Message);
                        exitCode = ApplicationShutdownFailureExitCode;
                    }
                }
            }

            if (pauseBeforeExit)
            {
                Console.WriteLine();
                Console.Write("Press Enter to close...");
                Console.ReadLine();
            }

            return exitCode;
        }

        internal static bool ShouldPauseAfterInvalidInput(
            string[] arguments,
            bool isInputRedirected,
            bool isUserInteractive)
        {
            return isUserInteractive
                && !isInputRedirected
                && (arguments == null || arguments.Length == 0);
        }

        private static bool OfferEsapiCheckAfterNoArgumentLaunch(string[] arguments)
        {
            if (!ShouldPauseAfterInvalidInput(
                arguments,
                Console.IsInputRedirected,
                Environment.UserInteractive))
            {
                return false;
            }

            Console.WriteLine();
            Console.WriteLine(BatchCommandLine.EsapiStartupCheckUsage);
            Console.Write(
                "Type T and press Enter to test ESAPI startup, or press Enter to close: ");
            return string.Equals(
                Console.ReadLine(),
                "T",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
