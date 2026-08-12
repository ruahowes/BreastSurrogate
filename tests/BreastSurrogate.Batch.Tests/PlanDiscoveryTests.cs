using System;
using BreastSurrogate.Batch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Batch.Tests
{
    [TestClass]
    public class PlanDiscoveryTests
    {
        private const string TokenPattern =
            "(?:^|[ _-])(PPHYS|PHYS)(?:$|[ _-])";

        [TestMethod]
        public void UniqueClinicalAndPhysicsBranchesAreSelectedIndependently()
        {
            PatientDiscoverySnapshot patient = Patient(
                Course("1", Plan("CLINICAL", PlanApprovalKind.Other)),
                PlanningCourse("PLANNING", "REJECTED", "REVIEWED",
                    Point(1.0, 2.0, 3.0)),
                Course("PPHYS RT BRST",
                    Plan("PPHYS RT BRST", PlanApprovalKind.Other)));

            PatientPlanDiscoveryResult result = Discover(patient);

            Assert.IsTrue(result.Clinical.IsSelected, result.Clinical.Reason);
            Assert.AreEqual("PLANNING", result.Clinical.CourseId);
            Assert.AreEqual("REVIEWED", result.Clinical.PlanId);
            Assert.AreEqual(PlanDiscoveryMethod.Automatic, result.Clinical.Method);
            Assert.IsTrue(result.Clinical.IsocentreValidation.IsValid);
            Assert.IsTrue(result.Physics.IsSelected, result.Physics.Reason);
            Assert.AreEqual("PPHYS RT BRST", result.Physics.CourseId);
            Assert.AreEqual("PPHYS RT BRST", result.Physics.PlanId);
        }

        [TestMethod]
        public void MultipleEligiblePlanningCoursesAreAmbiguous()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(
                PlanningCourse("PLANNING A", "R1", "V1", Point(0, 0, 0)),
                PlanningCourse("PLANNING B", "R2", "V2", Point(0, 0, 0))));

            Assert.AreEqual(PlanDiscoveryStatus.Ambiguous, result.Clinical.Status);
            Assert.IsFalse(result.Clinical.HasResolvedPlan);
            StringAssert.Contains(result.Clinical.Reason, "Multiple planning courses");
        }

        [TestMethod]
        public void ExactPlanningCourseOverrideDisambiguatesCourses()
        {
            PatientDiscoverySnapshot patient = Patient(
                PlanningCourse("PLANNING A", "R1", "V1", Point(0, 0, 0)),
                PlanningCourse("PLANNING B", "R2", "V2", Point(1, 0, 0)));

            PatientPlanDiscoveryResult result = Discover(
                patient,
                "PLANNING B",
                null,
                null);

            Assert.IsTrue(result.Clinical.IsSelected, result.Clinical.Reason);
            Assert.AreEqual("PLANNING B", result.Clinical.CourseId);
            Assert.AreEqual("V2", result.Clinical.PlanId);
            Assert.AreEqual(PlanDiscoveryMethod.ExactCourseOverride, result.Clinical.Method);
        }

        [TestMethod]
        public void ExactOverridesAreCaseSensitive()
        {
            PatientPlanDiscoveryResult result = Discover(
                Patient(PlanningCourse("PLANNING", "R", "V", Point(0, 0, 0))),
                "planning",
                null,
                null);

            Assert.AreEqual(PlanDiscoveryStatus.Missing, result.Clinical.Status);
            StringAssert.Contains(result.Clinical.Reason, "not found");
        }

        [TestMethod]
        public void MultipleReviewedPlansRemainAmbiguousWithCourseOverride()
        {
            CourseDiscoverySnapshot planning = Course(
                "PLANNING",
                Plan("REJECTED", PlanApprovalKind.Rejected),
                Plan("REVIEWED A", PlanApprovalKind.Reviewed, Point(0, 0, 0)),
                Plan("REVIEWED B", PlanApprovalKind.Reviewed, Point(0, 0, 0)));

            PatientPlanDiscoveryResult result = Discover(
                Patient(planning),
                "PLANNING",
                null,
                null);

            Assert.AreEqual(PlanDiscoveryStatus.Ambiguous, result.Clinical.Status);
            Assert.AreEqual("PLANNING", result.Clinical.CourseId);
            Assert.IsNull(result.Clinical.PlanId);
            StringAssert.Contains(result.Clinical.Reason, "multiple reviewed");
        }

        [TestMethod]
        public void OverriddenPlanningCourseStillRequiresRejectedPlan()
        {
            PatientPlanDiscoveryResult result = Discover(
                Patient(Course("PLANNING",
                    Plan("REVIEWED", PlanApprovalKind.Reviewed, Point(0, 0, 0)))),
                "PLANNING",
                null,
                null);

            Assert.AreEqual(PlanDiscoveryStatus.Unsupported, result.Clinical.Status);
            StringAssert.Contains(result.Clinical.Reason, "no rejected");
        }

        [TestMethod]
        public void ClinicalFallsBackFromXPrefixedRejectedIdWhenNoReviewedPlanExists()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(Course(
                "PLANNING",
                Plan("xL BRST", PlanApprovalKind.Rejected),
                Plan("L BRST", PlanApprovalKind.PlanningApproved, Point(1, 2, 3)),
                Plan("L BRST DC", PlanApprovalKind.Other, Point(1, 2, 3)))));

            Assert.IsTrue(result.Clinical.IsSelected, result.Clinical.Reason);
            Assert.AreEqual("L BRST", result.Clinical.PlanId);
            Assert.AreEqual(
                PlanDiscoveryMethod.RejectedPlanIdFallback,
                result.Clinical.Method);
        }

        [TestMethod]
        public void ReviewedClinicalPlanTakesPriorityOverRejectedIdFallback()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(Course(
                "PLANNING",
                Plan("xL BRST", PlanApprovalKind.Rejected),
                Plan("L BRST", PlanApprovalKind.PlanningApproved, Point(1, 2, 3)),
                Plan("REVIEWED", PlanApprovalKind.Reviewed, Point(4, 5, 6)))));

            Assert.IsTrue(result.Clinical.IsSelected, result.Clinical.Reason);
            Assert.AreEqual("REVIEWED", result.Clinical.PlanId);
            Assert.AreEqual(PlanDiscoveryMethod.Automatic, result.Clinical.Method);
        }

        [TestMethod]
        public void ClinicalRejectedIdFallbackRequiresLeadingXAndUniqueExactMatch()
        {
            PatientPlanDiscoveryResult noPrefix = Discover(Patient(Course(
                "PLANNING",
                Plan("OLD L BRST", PlanApprovalKind.Rejected),
                Plan("L BRST", PlanApprovalKind.PlanningApproved, Point(0, 0, 0)))));
            Assert.AreEqual(PlanDiscoveryStatus.Missing, noPrefix.Clinical.Status);

            PatientPlanDiscoveryResult ambiguous = Discover(Patient(Course(
                "PLANNING",
                Plan("xL BRST", PlanApprovalKind.Rejected),
                Plan("xR BRST", PlanApprovalKind.Rejected),
                Plan("L BRST", PlanApprovalKind.Other, Point(0, 0, 0)),
                Plan("R BRST", PlanApprovalKind.Other, Point(0, 0, 0)))));
            Assert.AreEqual(PlanDiscoveryStatus.Ambiguous, ambiguous.Clinical.Status);
            StringAssert.Contains(ambiguous.Clinical.Reason, "matched multiple");
        }

        [TestMethod]
        public void ReviewedPlanWithoutTreatmentIsocentreIsUnsupportedButIdsRemain()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(
                PlanningCourse("PLANNING", "REJECTED", "REVIEWED")));

            Assert.AreEqual(PlanDiscoveryStatus.Unsupported, result.Clinical.Status);
            Assert.IsTrue(result.Clinical.HasResolvedPlan);
            Assert.AreEqual("REVIEWED", result.Clinical.PlanId);
            StringAssert.Contains(result.Clinical.Reason, "no non-setup");
        }

        [TestMethod]
        public void MultipleDistinctIsocentresAreUnsupported()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(
                PlanningCourse("PLANNING", "REJECTED", "REVIEWED",
                    Point(0, 0, 0),
                    Point(0.011, 0, 0))));

            Assert.AreEqual(PlanDiscoveryStatus.Unsupported, result.Clinical.Status);
            Assert.IsTrue(result.Clinical.HasResolvedPlan);
            StringAssert.Contains(result.Clinical.Reason, "multiple distinct");
        }

        [TestMethod]
        public void IsocentresWithinToleranceProduceOrderIndependentMean()
        {
            IsocentreValidationResult first = PlanDiscoveryService.ValidateSingleIsocentre(
                new[] { Point(0.008, 2, 3), Point(0, 2, 3) },
                PlanDiscoveryService.DistinctIsocentreToleranceMm);
            IsocentreValidationResult reversed = PlanDiscoveryService.ValidateSingleIsocentre(
                new[] { Point(0, 2, 3), Point(0.008, 2, 3) },
                PlanDiscoveryService.DistinctIsocentreToleranceMm);

            Assert.IsTrue(first.IsValid);
            Assert.AreEqual(0.004, first.Isocentre.X, 1e-12);
            Assert.AreEqual(first.Isocentre.X, reversed.Isocentre.X, 0.0);
            Assert.AreEqual(first.Isocentre.Y, reversed.Isocentre.Y, 0.0);
            Assert.AreEqual(first.Isocentre.Z, reversed.Isocentre.Z, 0.0);
        }

        [TestMethod]
        public void IsocentresExactlyAtToleranceAreOneIsocentre()
        {
            IsocentreValidationResult result = PlanDiscoveryService.ValidateSingleIsocentre(
                new[] { Point(0, 0, 0), Point(0.01, 0, 0) },
                PlanDiscoveryService.DistinctIsocentreToleranceMm);

            Assert.IsTrue(result.IsValid, result.Reason);
            Assert.AreEqual(0.005, result.Isocentre.X, 1e-12);
        }

        [TestMethod]
        public void HistoricPhysAndPphysTokensMatchOnlyAtConfiguredBoundaries()
        {
            PatientPlanDiscoveryResult historic = Discover(Patient(
                Course("PHYS-RT BRST", Plan("PHYS_PLAN", PlanApprovalKind.Other))));
            Assert.IsTrue(historic.Physics.IsSelected, historic.Physics.Reason);

            PatientPlanDiscoveryResult partial = Discover(Patient(
                Course("BIOPHYSICS", Plan("BIOPHYSICS", PlanApprovalKind.Other))));
            Assert.AreEqual(PlanDiscoveryStatus.Missing, partial.Physics.Status);
        }

        [TestMethod]
        public void PhysicsFallsBackToUniquePphyTokenInCourseAndPlan()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(
                Course("PPHY RT BRST",
                    Plan("PPHY RT BRST", PlanApprovalKind.PlanningApproved))));

            Assert.IsTrue(result.Physics.IsSelected, result.Physics.Reason);
            Assert.AreEqual("PPHY RT BRST", result.Physics.CourseId);
            Assert.AreEqual("PPHY RT BRST", result.Physics.PlanId);
            Assert.AreEqual(
                PlanDiscoveryMethod.SimilarPhysicsTokenFallback,
                result.Physics.Method);
        }

        [TestMethod]
        public void ExactPhysicsTokenTakesPriorityOverSimilarToken()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(
                Course("PPHYS RT BRST", Plan("PPHYS PLAN", PlanApprovalKind.Other)),
                Course("PPHY RT BRST", Plan("PPHY PLAN", PlanApprovalKind.PlanningApproved))));

            Assert.IsTrue(result.Physics.IsSelected, result.Physics.Reason);
            Assert.AreEqual("PPHYS RT BRST", result.Physics.CourseId);
            Assert.AreEqual(PlanDiscoveryMethod.Automatic, result.Physics.Method);
        }

        [TestMethod]
        public void UniquePlanningApprovedPlanDisambiguatesSimilarPhysicsPlans()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(Course(
                "PPHYS",
                Plan("PPHY A", PlanApprovalKind.Other),
                Plan("PPHY B", PlanApprovalKind.PlanningApproved))));

            Assert.IsTrue(result.Physics.IsSelected, result.Physics.Reason);
            Assert.AreEqual("PPHY B", result.Physics.PlanId);
            Assert.AreEqual(
                PlanDiscoveryMethod.SimilarPhysicsTokenFallback,
                result.Physics.Method);
        }

        [TestMethod]
        public void MultipleSimilarPhysicsPlansRemainAmbiguousWithoutApprovalPreference()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(Course(
                "PPHYS",
                Plan("PPHY A", PlanApprovalKind.Other),
                Plan("PPHY B", PlanApprovalKind.Other))));

            Assert.AreEqual(PlanDiscoveryStatus.Ambiguous, result.Physics.Status);
            StringAssert.Contains(result.Physics.Reason, "Multiple external plans");
        }

        [TestMethod]
        public void MultiplePhysicsCoursesAreAmbiguousWithoutOverride()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(
                Course("PPHYS A", Plan("PPHYS A", PlanApprovalKind.Other)),
                Course("PHYS B", Plan("PHYS B", PlanApprovalKind.Other))));

            Assert.AreEqual(PlanDiscoveryStatus.Ambiguous, result.Physics.Status);
            Assert.IsFalse(result.Physics.HasResolvedPlan);
        }

        [TestMethod]
        public void ExactPhysicsPlanOverrideCanDisambiguatePhysicsCourses()
        {
            PatientPlanDiscoveryResult result = Discover(
                Patient(
                    Course("PPHYS A", Plan("PLAN A", PlanApprovalKind.Other)),
                    Course("PPHYS B", Plan("EXACT PLAN", PlanApprovalKind.Other))),
                null,
                null,
                "EXACT PLAN");

            Assert.IsTrue(result.Physics.IsSelected, result.Physics.Reason);
            Assert.AreEqual("PPHYS B", result.Physics.CourseId);
            Assert.AreEqual("EXACT PLAN", result.Physics.PlanId);
            Assert.AreEqual(PlanDiscoveryMethod.ExactPlanOverride, result.Physics.Method);
        }

        [TestMethod]
        public void ExactPhysicsCourseAndPlanOverridesBypassTokenConvention()
        {
            PatientPlanDiscoveryResult result = Discover(
                Patient(Course("SPECIAL COURSE",
                    Plan("SPECIAL PLAN", PlanApprovalKind.Other))),
                null,
                "SPECIAL COURSE",
                "SPECIAL PLAN");

            Assert.IsTrue(result.Physics.IsSelected, result.Physics.Reason);
            Assert.AreEqual(PlanDiscoveryMethod.ExactCourseAndPlanOverride,
                result.Physics.Method);
        }

        [TestMethod]
        public void MultiplePhysicsPlansAreAmbiguous()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(
                Course("PPHYS",
                    Plan("PPHYS A", PlanApprovalKind.Other),
                    Plan("PPHYS B", PlanApprovalKind.Other))));

            Assert.AreEqual(PlanDiscoveryStatus.Ambiguous, result.Physics.Status);
            Assert.AreEqual("PPHYS", result.Physics.CourseId);
            Assert.IsNull(result.Physics.PlanId);
        }

        [TestMethod]
        public void ClinicalFailureDoesNotDiscardPhysicsSelection()
        {
            PatientPlanDiscoveryResult result = Discover(Patient(
                Course("PLANNING", Plan("UNAPPROVED", PlanApprovalKind.Other)),
                Course("PPHYS", Plan("PPHYS", PlanApprovalKind.Other))));

            Assert.AreEqual(PlanDiscoveryStatus.Missing, result.Clinical.Status);
            Assert.IsTrue(result.Physics.IsSelected, result.Physics.Reason);
        }

        private static PatientPlanDiscoveryResult Discover(
            PatientDiscoverySnapshot patient,
            string planningOverride = null,
            string physicsCourseOverride = null,
            string physicsPlanOverride = null)
        {
            var input = new PatientInputRow(
                2,
                "PATIENT",
                planningOverride,
                physicsCourseOverride,
                physicsPlanOverride);
            var configuration = new CourseDiscoveryConfiguration(
                "PLANNING",
                true,
                1,
                TokenPattern,
                TokenPattern);
            return PlanDiscoveryService.Discover(patient, input, configuration);
        }

        private static PatientDiscoverySnapshot Patient(
            params CourseDiscoverySnapshot[] courses)
        {
            return new PatientDiscoverySnapshot(courses);
        }

        private static CourseDiscoverySnapshot PlanningCourse(
            string courseId,
            string rejectedPlanId,
            string reviewedPlanId,
            params DiscoveryPoint3D[] isocentres)
        {
            return Course(
                courseId,
                Plan(rejectedPlanId, PlanApprovalKind.Rejected),
                Plan(reviewedPlanId, PlanApprovalKind.Reviewed, isocentres));
        }

        private static CourseDiscoverySnapshot Course(
            string id,
            params ExternalPlanDiscoverySnapshot[] plans)
        {
            return new CourseDiscoverySnapshot(id, plans);
        }

        private static ExternalPlanDiscoverySnapshot Plan(
            string id,
            PlanApprovalKind approval,
            params DiscoveryPoint3D[] isocentres)
        {
            return new ExternalPlanDiscoverySnapshot(id, approval, isocentres);
        }

        private static DiscoveryPoint3D Point(double x, double y, double z)
        {
            return new DiscoveryPoint3D(x, y, z);
        }
    }
}
