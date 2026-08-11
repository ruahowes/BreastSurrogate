using System;
using System.IO;
using BreastSurrogate.Batch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Batch.Tests
{
    [TestClass]
    public class ConsoleProgressReporterTests
    {
        [TestMethod]
        public void RedirectedOutputWritesReadableSanitizedLines()
        {
            var writer = new StringWriter();
            writer.NewLine = "\n";
            var reporter = new ConsoleProgressReporter(writer, false, 10);

            reporter.Report(1, 4, "Patient\r\nA");
            reporter.Report(4, 4, "Complete");

            string output = writer.ToString();
            StringAssert.Contains(output, "[###       ] 1/4 025% Patient  A");
            StringAssert.Contains(output, "[##########] 4/4 100% Complete");
            Assert.IsFalse(output.Contains("\r"));
        }

        [TestMethod]
        public void RedirectedOutputSuppressesDuplicateCompletedCount()
        {
            var writer = new StringWriter();
            var reporter = new ConsoleProgressReporter(writer, false, 10);

            reporter.Report(1, 2, "First");
            reporter.Report(1, 2, "Duplicate");

            StringAssert.Contains(writer.ToString(), "First");
            Assert.IsFalse(writer.ToString().Contains("Duplicate"));
        }

        [TestMethod]
        public void InteractiveOutputUsesCarriageReturnAndTerminatesAtCompletion()
        {
            var writer = new StringWriter();
            var reporter = new ConsoleProgressReporter(writer, true, 5);

            reporter.Report(0, 2, "Starting a long description");
            reporter.Report(2, 2, "Done");

            string output = writer.ToString();
            Assert.IsTrue(output.StartsWith("\r", StringComparison.Ordinal));
            Assert.IsTrue(output.EndsWith(Environment.NewLine, StringComparison.Ordinal));
            StringAssert.Contains(output, "[#####] 2/2 100% Done");
        }

        [TestMethod]
        public void InvalidCountsAndWidthAreRejected()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new ConsoleProgressReporter(new StringWriter(), false, 0));

            var reporter = new ConsoleProgressReporter(new StringWriter(), false);
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => reporter.Report(0, 0, null));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => reporter.Report(3, 2, null));
        }
    }
}
