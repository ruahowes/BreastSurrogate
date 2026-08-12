using BreastSurrogate.Batch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Batch.Tests
{
    [TestClass]
    public class ProgramTests
    {
        [TestMethod]
        public void NoArgumentInteractiveLaunchPauses()
        {
            Assert.IsTrue(Program.ShouldPauseAfterInvalidInput(
                new string[0],
                false,
                true));
        }

        [TestMethod]
        public void RedirectedOrNonInteractiveLaunchDoesNotPause()
        {
            Assert.IsFalse(Program.ShouldPauseAfterInvalidInput(
                new string[0],
                true,
                true));
            Assert.IsFalse(Program.ShouldPauseAfterInvalidInput(
                new string[0],
                false,
                false));
        }

        [TestMethod]
        public void LaunchWithArgumentsDoesNotPause()
        {
            Assert.IsFalse(Program.ShouldPauseAfterInvalidInput(
                new[] { "patients.csv" },
                false,
                true));
        }

        [TestMethod]
        public void PatientIdIsSanitizedForPerPatientLogFile()
        {
            string sanitized = Program.SanitizeFileName("PAT/01");

            Assert.IsFalse(sanitized.Contains("/"));
            Assert.AreNotEqual(string.Empty, sanitized);
        }
    }
}
