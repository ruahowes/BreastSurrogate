using System.IO;
using BreastSurrogate.Batch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Batch.Tests
{
    [TestClass]
    public class BatchOutputCsvTests
    {
        [TestMethod]
        public void OutputUsesStableColumnsInvariantNumbersAndQuotedText()
        {
            BatchConfiguration configuration;
            string error;
            Assert.IsTrue(BatchConfigurationLoader.TryParse(
                BatchConfigurationTests.ValidJson,
                Path.GetFullPath("."),
                "hash",
                path => true,
                out configuration,
                out error), error);

            BatchOutputSchema schema = BatchOutputSchema.Create(configuration);
            var row = new BatchOutputRow();
            row.SetText("PatientId", "Patient, One");
            row.SetMetric("gILF", "ValuePercent", AuditMetricOutput.Available(12.75, "%"));
            row.SetMetric(
                "Heart_Dmean_Gy",
                "Value",
                AuditMetricOutput.Unavailable(
                    AuditValueStatus.MissingData,
                    "Heart absent, metric unavailable"));

            var output = new StringWriter();
            var writer = new BatchOutputCsvWriter(output, schema);
            writer.WriteHeader();
            writer.WriteRow(row);

            string csv = output.ToString();
            StringAssert.Contains(
                csv,
                "ClinicalDiscoveryStatus,ClinicalDiscoveryMethod,ClinicalDiscoveryReason," +
                "PhysicsDiscoveryStatus,PhysicsDiscoveryMethod,PhysicsDiscoveryReason," +
                "ClinicalIsocentreXmm,ClinicalIsocentreYmm,ClinicalIsocentreZmm," +
                "ClinicalIsocentreReason");
            StringAssert.Contains(csv, "gILF_ValuePercent,gILF_Unit,gILF_Status,gILF_Reason");
            StringAssert.Contains(csv, "\"Patient, One\"");
            StringAssert.Contains(csv, "12.75,%,Available,");
            StringAssert.Contains(csv, ",,MissingData,\"Heart absent, metric unavailable\"");

            System.Collections.Generic.IList<CsvRecord> records;
            Assert.IsTrue(CsvCodec.TryRead(
                new StringReader(csv),
                out records,
                out error), error);
            int patientColumn = new System.Collections.Generic.List<string>(
                records[0].Fields).IndexOf("PatientId");
            Assert.AreEqual("Patient, One", records[1].Fields[patientColumn]);
        }

        [TestMethod]
        public void NonFiniteAvailableMetricIsRejected()
        {
            Assert.ThrowsException<System.ArgumentOutOfRangeException>(
                () => AuditMetricOutput.Available(double.NaN, "%"));
        }
    }
}
