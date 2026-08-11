using System.IO;
using BreastSurrogate.Batch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Batch.Tests
{
    [TestClass]
    public class PatientInputCsvTests
    {
        [TestMethod]
        public void QuotedFieldsAndRepeatedPatientWithDifferentOverridesAreAccepted()
        {
            const string csv =
                "PatientId,PlanningCourseId,PhysicsCourseId,PhysicsPlanId\r\n"
                + "\"Patient, One\",\"PLANNING\"\" A\",PPHYS A,PPHYS PLAN A\r\n"
                + "\"Patient, One\",PLANNING B,PPHYS B,PPHYS PLAN B\r\n";

            System.Collections.Generic.IList<PatientInputRow> rows;
            string error;
            bool loaded = PatientInputCsv.TryRead(
                new StringReader(csv),
                out rows,
                out error);

            Assert.IsTrue(loaded, error);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Patient, One", rows[0].PatientId);
            Assert.AreEqual("PLANNING\" A", rows[0].PlanningCourseId);
        }

        [TestMethod]
        public void ExactDuplicateRowIsRejected()
        {
            const string csv = "PatientId,PhysicsPlanId\nP1,PLAN\nP1,PLAN\n";
            System.Collections.Generic.IList<PatientInputRow> rows;
            string error;

            Assert.IsFalse(PatientInputCsv.TryRead(
                new StringReader(csv),
                out rows,
                out error));
            StringAssert.Contains(error, "duplicates");
        }

        [TestMethod]
        public void MalformedQuotedCsvReportsLine()
        {
            const string csv = "PatientId\n\"P1\n";
            System.Collections.Generic.IList<PatientInputRow> rows;
            string error;

            Assert.IsFalse(PatientInputCsv.TryRead(
                new StringReader(csv),
                out rows,
                out error));
            StringAssert.Contains(error, "line 2");
        }

        [TestMethod]
        public void OptionalOverridesMayBeBlank()
        {
            const string csv = "PatientId,PlanningCourseId,PhysicsCourseId,PhysicsPlanId\nP1,,,\n";
            System.Collections.Generic.IList<PatientInputRow> rows;
            string error;

            Assert.IsTrue(PatientInputCsv.TryRead(
                new StringReader(csv),
                out rows,
                out error), error);
            Assert.IsNull(rows[0].PlanningCourseId);
            Assert.IsNull(rows[0].PhysicsCourseId);
            Assert.IsNull(rows[0].PhysicsPlanId);
        }

        [TestMethod]
        public void QuotedIdentifierWhitespaceIsPreserved()
        {
            const string csv = "PatientId\n\" P1 \"\n";
            System.Collections.Generic.IList<PatientInputRow> rows;
            string error;

            Assert.IsTrue(PatientInputCsv.TryRead(
                new StringReader(csv),
                out rows,
                out error), error);
            Assert.AreEqual(" P1 ", rows[0].PatientId);
        }
    }
}
