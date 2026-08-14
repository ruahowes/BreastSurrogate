using System;
using System.IO;
using BreastSurrogate.Batch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Batch.Tests
{
    [TestClass]
    public class ProgramTests
    {
        [TestMethod]
        public void RunDirectoryIsTimestampedBelowExecutableDirectory()
        {
            string path = Program.GetRunDirectoryPath(
                @"C:\Apps\BreastSurrogate",
                new DateTime(2026, 8, 14, 9, 8, 7, 654));

            Assert.AreEqual(
                Path.Combine(
                    @"C:\Apps\BreastSurrogate",
                    "BreastSurrogateAudit_20260814_090807_654"),
                path);
        }

        [TestMethod]
        public void PatientIdIsSanitizedForPerPatientLogFile()
        {
            string sanitized = Program.SanitizeFileName("PAT/01");

            Assert.AreEqual("PAT[47]01", sanitized);
        }

        [TestMethod]
        public void EmptyPatientIdUsesSafeFallbackFileName()
        {
            Assert.AreEqual("patient", Program.SanitizeFileName(string.Empty));
        }
    }
}
