using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BreastSurrogate.Esapi.Esapi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Batch.Tests
{
    [TestClass]
    public class PatientAuditProcessorTests
    {
        [TestMethod]
        public void PopulatesIndependentPhysicsLegacyAndClinicalResults()
        {
            BatchConfiguration configuration = Configuration();
            var session = new FakeSession
            {
                Snapshot = SuccessfulSnapshot(),
                Physics = PhysicsWithUnavailableGeometry(),
                Clinical = Clinical(configuration)
            };

            PatientAuditResult result = new PatientAuditProcessor().Process(
                Row("P1"),
                configuration,
                "1.2.3",
                session);

            Assert.AreEqual("Unsupported", result.Row.GetValue("gILF_Status"));
            Assert.AreEqual(
                12.7,
                double.Parse(result.Row.GetValue("ILF_ValuePercent"), CultureInfo.InvariantCulture),
                1e-12);
            Assert.AreEqual("Available", result.Row.GetValue("ILF_Status"));
            Assert.AreEqual("15", result.Row.GetValue("Fractions"));
            Assert.AreEqual("Lung_L", result.Row.GetValue("ClinicalIpsilateralLungStructureId"));
            Assert.AreEqual("Heart", result.Row.GetValue("ClinicalHeartStructureId"));
            Assert.AreEqual(
                12.7,
                double.Parse(
                    result.Row.GetValue("IpsilateralLung_V8Gy_Percent_Value"),
                    CultureInfo.InvariantCulture),
                1e-12);
            Assert.AreEqual("Gy", result.Row.GetValue("Heart_Dmean_Gy_Unit"));
            Assert.AreEqual("1.2.3", result.Row.GetValue("ApplicationVersion"));
            Assert.IsTrue(result.HasFailures);
        }

        [TestMethod]
        public void DiscoveryFailureInPhysicsDoesNotDiscardClinicalMetrics()
        {
            BatchConfiguration configuration = Configuration();
            var session = new FakeSession
            {
                Snapshot = new PatientDiscoverySnapshot(new[]
                {
                    PlanningCourse()
                }),
                Clinical = Clinical(configuration)
            };

            PatientAuditResult result = new PatientAuditProcessor().Process(
                Row("P1"), configuration, "1", session);

            Assert.AreEqual("MissingData", result.Row.GetValue("gILF_Status"));
            Assert.AreEqual("Available", result.Row.GetValue("Heart_Dmean_Gy_Status"));
            Assert.AreEqual(0, session.PhysicsCalls);
            Assert.AreEqual(1, session.ClinicalCalls);
        }

        [TestMethod]
        public void RunnerContinuesAndDisposesEachOpenedSessionExactlyOnce()
        {
            BatchConfiguration configuration = Configuration();
            var first = new FakeSession
            {
                SnapshotException = new InvalidOperationException("snapshot failed")
            };
            var second = new FakeSession
            {
                Snapshot = new PatientDiscoverySnapshot(new CourseDiscoverySnapshot[0])
            };
            var source = new QueueSource(first, second);
            var csvText = new StringWriter();
            var logText = new StringWriter();
            int patientLogs = 0;

            BatchRunSummary summary = new BatchAuditRunner().Run(
                new[] { Row("P1"), Row("P2") },
                configuration,
                "1",
                source,
                new BatchOutputCsvWriter(csvText, BatchOutputSchema.Create(configuration)),
                logText,
                (index, input, lines) => patientLogs++,
                new ConsoleProgressReporter(new StringWriter(), false));

            Assert.AreEqual(2, summary.TotalRows);
            Assert.AreEqual(2, summary.RowsWithFailures);
            Assert.AreEqual(1, first.DisposeCalls);
            Assert.AreEqual(1, second.DisposeCalls);
            Assert.AreEqual(2, patientLogs);
            IList<CsvRecord> records;
            string error;
            Assert.IsTrue(CsvCodec.TryRead(
                new StringReader(csvText.ToString()),
                out records,
                out error), error);
            Assert.AreEqual(3, records.Count);
            StringAssert.Contains(logText.ToString(), "Batch summary");
        }

        [TestMethod]
        public void MissingPatientProducesRowAndLaterPatientsStillRun()
        {
            BatchConfiguration configuration = Configuration();
            var second = new FakeSession
            {
                Snapshot = new PatientDiscoverySnapshot(new CourseDiscoverySnapshot[0])
            };
            var source = new QueueSource(null, second);
            var csvText = new StringWriter();

            BatchRunSummary summary = new BatchAuditRunner().Run(
                new[] { Row("MISSING"), Row("P2") },
                configuration,
                "1",
                source,
                new BatchOutputCsvWriter(csvText, BatchOutputSchema.Create(configuration)),
                new StringWriter(),
                null,
                new ConsoleProgressReporter(new StringWriter(), false));

            Assert.AreEqual(2, summary.TotalRows);
            Assert.AreEqual(1, second.DisposeCalls);
            StringAssert.Contains(csvText.ToString(), "Patient was not found: MISSING");
        }

        [TestMethod]
        public void CloseFailureIsRecordedAndDoesNotTriggerSecondCloseAttempt()
        {
            BatchConfiguration configuration = Configuration();
            var session = new FakeSession
            {
                Snapshot = new PatientDiscoverySnapshot(new CourseDiscoverySnapshot[0]),
                DisposeException = new InvalidOperationException("close failed")
            };
            var csvText = new StringWriter();

            BatchRunSummary summary = new BatchAuditRunner().Run(
                new[] { Row("P1") },
                configuration,
                "1",
                new QueueSource(session),
                new BatchOutputCsvWriter(csvText, BatchOutputSchema.Create(configuration)),
                new StringWriter(),
                null,
                new ConsoleProgressReporter(new StringWriter(), false));

            Assert.AreEqual(1, session.DisposeCalls);
            Assert.AreEqual(1, summary.RowsWithFailures);
            StringAssert.Contains(csvText.ToString(), "Patient closure failed: close failed");
        }

        private static BatchConfiguration Configuration()
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
            return configuration;
        }

        private static PatientInputRow Row(string patientId)
        {
            return new PatientInputRow(2, patientId, null, null, null);
        }

        private static PatientDiscoverySnapshot SuccessfulSnapshot()
        {
            return new PatientDiscoverySnapshot(new[]
            {
                PlanningCourse(),
                new CourseDiscoverySnapshot("PPHYS RT BRST", new[]
                {
                    new ExternalPlanDiscoverySnapshot(
                        "PPHYS PLAN",
                        PlanApprovalKind.Other,
                        new DiscoveryPoint3D[0])
                })
            });
        }

        private static CourseDiscoverySnapshot PlanningCourse()
        {
            return new CourseDiscoverySnapshot("PLANNING", new[]
            {
                new ExternalPlanDiscoverySnapshot(
                    "OLD", PlanApprovalKind.Rejected, new DiscoveryPoint3D[0]),
                new ExternalPlanDiscoverySnapshot(
                    "REVIEWED", PlanApprovalKind.Reviewed,
                    new[] { new DiscoveryPoint3D(1, 2, 3) })
            });
        }

        private static PhysicsPlanMetricResult PhysicsWithUnavailableGeometry()
        {
            var geometric = new BreastSurrogateCalculationResult(
                "P1",
                "PPHYS PLAN",
                2,
                null,
                null,
                null,
                null,
                SurrogateMetricResult.Unavailable("gILF", null, "unsupported geometry"),
                SurrogateMetricResult.Unavailable("gHIF", null, "unsupported geometry"),
                "unsupported geometry");
            var legacy = new LegacyPlanMetricResult(
                MetricCalculationResult.Available("ILF", "ILF", 12.7, "%", null),
                MetricCalculationResult.Available("HIF", "HIF", 2.5, "%", null),
                "IPS LUNG",
                "Heart",
                "ILF",
                "HIF");
            return new PhysicsPlanMetricResult(geometric, legacy);
        }

        private static ReviewedPlanMetricResult Clinical(BatchConfiguration configuration)
        {
            return new ReviewedPlanMetricResult(
                "REVIEWED",
                15,
                "Lung_L",
                "Heart",
                new[]
                {
                    MetricCalculationResult.Available(
                        configuration.Metrics[0].Name, "Lung_L", 12.7, "%", "Gy"),
                    MetricCalculationResult.Available(
                        configuration.Metrics[1].Name, "Lung_L", 8.2, "%", "Gy"),
                    MetricCalculationResult.Available(
                        configuration.Metrics[2].Name, "Heart", 1.25, "Gy", "cGy")
                });
        }

        private sealed class FakeSession : IPatientAuditSession
        {
            public PatientDiscoverySnapshot Snapshot { get; set; }
            public Exception SnapshotException { get; set; }
            public Exception DisposeException { get; set; }
            public PhysicsPlanMetricResult Physics { get; set; }
            public ReviewedPlanMetricResult Clinical { get; set; }
            public int DisposeCalls { get; private set; }
            public int PhysicsCalls { get; private set; }
            public int ClinicalCalls { get; private set; }

            public PatientDiscoverySnapshot CreateDiscoverySnapshot()
            {
                if (SnapshotException != null) throw SnapshotException;
                return Snapshot;
            }

            public PhysicsPlanMetricResult CalculatePhysics(string courseId, string planId)
            {
                PhysicsCalls++;
                return Physics;
            }

            public ReviewedPlanMetricResult CalculateClinical(
                string courseId,
                string planId,
                DiscoveryPoint3D isocentre,
                IList<ReviewedPlanMetricRequest> requests,
                double binWidthGy)
            {
                ClinicalCalls++;
                return Clinical;
            }

            public void Dispose()
            {
                DisposeCalls++;
                if (DisposeException != null) throw DisposeException;
            }
        }

        private sealed class QueueSource : IPatientAuditSource
        {
            private readonly Queue<FakeSession> _sessions;

            public QueueSource(params FakeSession[] sessions)
            {
                _sessions = new Queue<FakeSession>(sessions);
            }

            public IPatientAuditSession OpenPatient(string patientId)
            {
                FakeSession session = _sessions.Dequeue();
                if (session == null) throw new PatientNotFoundException(patientId);
                return session;
            }
        }
    }
}
