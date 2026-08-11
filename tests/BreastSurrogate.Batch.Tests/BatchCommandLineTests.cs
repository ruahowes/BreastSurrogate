using System;
using System.Collections.Generic;
using System.IO;
using BreastSurrogate.Batch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Batch.Tests
{
    [TestClass]
    public class BatchCommandLineTests
    {
        [TestMethod]
        public void RequiresExactlyTwoArguments()
        {
            BatchCommandLineOptions options;
            string error;

            bool parsed = BatchCommandLine.TryParse(
                new[] { "patients.csv" },
                path => true,
                out options,
                out error);

            Assert.IsFalse(parsed);
            Assert.IsNull(options);
            StringAssert.Contains(error, BatchCommandLine.Usage);
        }

        [TestMethod]
        public void ReportsWhichRequiredFileIsMissing()
        {
            BatchCommandLineOptions options;
            string error;
            string patients = Path.GetFullPath("patients.csv");
            string config = Path.GetFullPath("config.json");

            bool missingPatients = BatchCommandLine.TryParse(
                new[] { patients, config },
                path => false,
                out options,
                out error);
            Assert.IsFalse(missingPatients);
            StringAssert.Contains(error, "Patient-list CSV");

            bool missingConfig = BatchCommandLine.TryParse(
                new[] { patients, config },
                path => string.Equals(path, patients, StringComparison.Ordinal),
                out options,
                out error);
            Assert.IsFalse(missingConfig);
            StringAssert.Contains(error, "JSON configuration");
        }

        [TestMethod]
        public void SuccessfulParseReturnsAbsolutePaths()
        {
            var existing = new HashSet<string>(StringComparer.Ordinal)
            {
                Path.GetFullPath("patients.csv"),
                Path.GetFullPath("config.json")
            };
            BatchCommandLineOptions options;
            string error;

            bool parsed = BatchCommandLine.TryParse(
                new[] { " patients.csv ", "config.json" },
                existing.Contains,
                out options,
                out error);

            Assert.IsTrue(parsed);
            Assert.IsNull(error);
            Assert.AreEqual(Path.GetFullPath("patients.csv"), options.PatientListPath);
            Assert.AreEqual(Path.GetFullPath("config.json"), options.ConfigurationPath);
        }

        [TestMethod]
        public void NullFileProbeIsRejected()
        {
            BatchCommandLineOptions options;
            string error;

            Assert.ThrowsException<ArgumentNullException>(
                () => BatchCommandLine.TryParse(
                    new[] { "patients.csv", "config.json" },
                    null,
                    out options,
                    out error));
        }

        [TestMethod]
        public void InvalidPathIsReportedWithoutThrowing()
        {
            BatchCommandLineOptions options;
            string error;

            bool parsed = BatchCommandLine.TryParse(
                new[] { "\0", "config.json" },
                path => true,
                out options,
                out error);

            Assert.IsFalse(parsed);
            Assert.IsNull(options);
            StringAssert.Contains(error, "invalid");
        }

        [TestMethod]
        public void EsapiStartupCheckIsAnExplicitCaseInsensitiveMode()
        {
            Assert.IsTrue(BatchCommandLine.IsEsapiStartupCheck(
                new[] { "--check-esapi" }));
            Assert.IsTrue(BatchCommandLine.IsEsapiStartupCheck(
                new[] { "--CHECK-ESAPI" }));
            Assert.IsFalse(BatchCommandLine.IsEsapiStartupCheck(new string[0]));
            Assert.IsFalse(BatchCommandLine.IsEsapiStartupCheck(
                new[] { "--check-esapi", "extra" }));
        }
    }
}
