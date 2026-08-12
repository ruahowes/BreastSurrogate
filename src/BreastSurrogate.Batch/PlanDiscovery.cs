using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace BreastSurrogate.Batch
{
    public enum PlanApprovalKind
    {
        Other,
        Rejected,
        Reviewed,
        PlanningApproved
    }

    public enum PlanDiscoveryStatus
    {
        Selected,
        Missing,
        Ambiguous,
        Unsupported
    }

    public enum PlanDiscoveryMethod
    {
        None,
        Automatic,
        RejectedPlanIdFallback,
        SimilarPhysicsTokenFallback,
        ExactCourseOverride,
        ExactCourseOverrideWithRejectedPlanIdFallback,
        ExactPlanOverride,
        ExactCourseAndPlanOverride
    }

    public sealed class DiscoveryPoint3D
    {
        public DiscoveryPoint3D(double x, double y, double z)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
            {
                throw new ArgumentException("Discovery point coordinates must be finite.");
            }

            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; private set; }
        public double Y { get; private set; }
        public double Z { get; private set; }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public sealed class ExternalPlanDiscoverySnapshot
    {
        public ExternalPlanDiscoverySnapshot(
            string id,
            PlanApprovalKind approval,
            IEnumerable<DiscoveryPoint3D> treatmentIsocentres)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Plan ID is required.", "id");
            }

            Id = id;
            Approval = approval;
            TreatmentIsocentres = new List<DiscoveryPoint3D>(
                treatmentIsocentres ?? Enumerable.Empty<DiscoveryPoint3D>())
                .AsReadOnly();
            if (TreatmentIsocentres.Any(point => point == null))
            {
                throw new ArgumentException("Treatment isocentres cannot contain null.");
            }
        }

        public string Id { get; private set; }
        public PlanApprovalKind Approval { get; private set; }
        public IList<DiscoveryPoint3D> TreatmentIsocentres { get; private set; }
    }

    public sealed class CourseDiscoverySnapshot
    {
        public CourseDiscoverySnapshot(
            string id,
            IEnumerable<ExternalPlanDiscoverySnapshot> externalPlans)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Course ID is required.", "id");
            }

            Id = id;
            ExternalPlans = new List<ExternalPlanDiscoverySnapshot>(
                externalPlans ?? Enumerable.Empty<ExternalPlanDiscoverySnapshot>())
                .AsReadOnly();
            if (ExternalPlans.Any(plan => plan == null))
            {
                throw new ArgumentException("External plans cannot contain null.");
            }
        }

        public string Id { get; private set; }
        public IList<ExternalPlanDiscoverySnapshot> ExternalPlans { get; private set; }
    }

    public sealed class PatientDiscoverySnapshot
    {
        public PatientDiscoverySnapshot(IEnumerable<CourseDiscoverySnapshot> courses)
        {
            Courses = new List<CourseDiscoverySnapshot>(
                courses ?? Enumerable.Empty<CourseDiscoverySnapshot>())
                .AsReadOnly();
            if (Courses.Any(course => course == null))
            {
                throw new ArgumentException("Courses cannot contain null.");
            }
        }

        public IList<CourseDiscoverySnapshot> Courses { get; private set; }
    }

    public sealed class IsocentreValidationResult
    {
        private IsocentreValidationResult(
            bool isValid,
            DiscoveryPoint3D isocentre,
            string reason,
            int beamCount)
        {
            IsValid = isValid;
            Isocentre = isocentre;
            Reason = reason;
            BeamCount = beamCount;
        }

        public bool IsValid { get; private set; }
        public DiscoveryPoint3D Isocentre { get; private set; }
        public string Reason { get; private set; }
        public int BeamCount { get; private set; }

        internal static IsocentreValidationResult Valid(
            DiscoveryPoint3D isocentre,
            int beamCount)
        {
            return new IsocentreValidationResult(true, isocentre, null, beamCount);
        }

        internal static IsocentreValidationResult Invalid(string reason, int beamCount)
        {
            return new IsocentreValidationResult(false, null, reason, beamCount);
        }
    }

    public sealed class PlanBranchDiscoveryResult
    {
        internal PlanBranchDiscoveryResult(
            PlanDiscoveryStatus status,
            PlanDiscoveryMethod method,
            string courseId,
            string planId,
            string reason,
            IEnumerable<string> diagnostics,
            IsocentreValidationResult isocentreValidation)
        {
            Status = status;
            Method = method;
            CourseId = courseId;
            PlanId = planId;
            Reason = reason;
            Diagnostics = new List<string>(diagnostics).AsReadOnly();
            IsocentreValidation = isocentreValidation;
        }

        public PlanDiscoveryStatus Status { get; private set; }
        public PlanDiscoveryMethod Method { get; private set; }
        public string CourseId { get; private set; }
        public string PlanId { get; private set; }
        public string Reason { get; private set; }
        public IList<string> Diagnostics { get; private set; }
        public IsocentreValidationResult IsocentreValidation { get; private set; }
        public bool IsSelected { get { return Status == PlanDiscoveryStatus.Selected; } }
        public bool HasResolvedPlan { get { return CourseId != null && PlanId != null; } }
    }

    public sealed class PatientPlanDiscoveryResult
    {
        public PatientPlanDiscoveryResult(
            PlanBranchDiscoveryResult clinical,
            PlanBranchDiscoveryResult physics)
        {
            if (clinical == null)
            {
                throw new ArgumentNullException("clinical");
            }

            if (physics == null)
            {
                throw new ArgumentNullException("physics");
            }

            Clinical = clinical;
            Physics = physics;
        }

        public PlanBranchDiscoveryResult Clinical { get; private set; }
        public PlanBranchDiscoveryResult Physics { get; private set; }
    }

    public static class PlanDiscoveryService
    {
        public const double DistinctIsocentreToleranceMm = 0.01;

        public static PatientPlanDiscoveryResult Discover(
            PatientDiscoverySnapshot patient,
            PatientInputRow input,
            CourseDiscoveryConfiguration configuration)
        {
            if (patient == null)
            {
                throw new ArgumentNullException("patient");
            }

            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            return new PatientPlanDiscoveryResult(
                DiscoverClinical(patient, input.PlanningCourseId, configuration),
                DiscoverPhysics(
                    patient,
                    input.PhysicsCourseId,
                    input.PhysicsPlanId,
                    configuration));
        }

        private static PlanBranchDiscoveryResult DiscoverClinical(
            PatientDiscoverySnapshot patient,
            string courseOverride,
            CourseDiscoveryConfiguration configuration)
        {
            var diagnostics = new List<string>();
            List<CourseDiscoverySnapshot> namedCourses = patient.Courses
                .Where(course => course.Id.IndexOf(
                    configuration.PlanningCourseIdContains,
                    StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            foreach (CourseDiscoverySnapshot course in namedCourses.OrderBy(item => item.Id))
            {
                diagnostics.Add(FormatCourseSignature(course));
            }

            CourseDiscoverySnapshot selectedCourse;
            PlanDiscoveryMethod method;
            if (courseOverride != null)
            {
                List<CourseDiscoverySnapshot> exact = patient.Courses
                    .Where(course => string.Equals(
                        course.Id,
                        courseOverride,
                        StringComparison.Ordinal))
                    .ToList();
                if (exact.Count == 0)
                {
                    return Failure(PlanDiscoveryStatus.Missing,
                        "Exact planning-course override was not found: " + courseOverride,
                        diagnostics);
                }

                if (exact.Count > 1)
                {
                    return Failure(PlanDiscoveryStatus.Ambiguous,
                        "Exact planning-course override matched multiple courses: " + courseOverride,
                        diagnostics);
                }

                selectedCourse = exact[0];
                method = PlanDiscoveryMethod.ExactCourseOverride;
                if (selectedCourse.Id.IndexOf(
                    configuration.PlanningCourseIdContains,
                    StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return CourseFailure(selectedCourse, PlanDiscoveryStatus.Unsupported, method,
                        "Overridden course ID does not contain the configured PLANNING text.", diagnostics);
                }
            }
            else
            {
                List<CourseDiscoverySnapshot> eligible = namedCourses
                    .Where(course => RejectedCount(course) >= 1 && ReviewedCount(course) == 1)
                    .ToList();
                if (eligible.Count == 0)
                {
                    eligible = namedCourses
                        .Where(course => RejectedCount(course) >= 1
                            && ReviewedCount(course) == 0
                            && FindRejectedIdFallbackPlans(course).Count > 0)
                        .ToList();
                }

                if (eligible.Count == 0)
                {
                    return Failure(PlanDiscoveryStatus.Missing,
                        "No planning course has a selectable reviewed plan or a unique plan matching an x-prefixed rejected plan ID.",
                        diagnostics);
                }

                if (eligible.Count > 1)
                {
                    return Failure(PlanDiscoveryStatus.Ambiguous,
                        "Multiple planning courses have the required approval-status signature.",
                        diagnostics);
                }

                selectedCourse = eligible[0];
                method = PlanDiscoveryMethod.Automatic;
            }

            if (configuration.RequireRejectedPlan && RejectedCount(selectedCourse) == 0)
            {
                return CourseFailure(selectedCourse, PlanDiscoveryStatus.Unsupported, method,
                    "Selected planning course has no rejected external plan.", diagnostics);
            }

            List<ExternalPlanDiscoverySnapshot> reviewed = selectedCourse.ExternalPlans
                .Where(plan => plan.Approval == PlanApprovalKind.Reviewed)
                .ToList();
            if (reviewed.Count == 0)
            {
                List<ExternalPlanDiscoverySnapshot> fallback =
                    FindRejectedIdFallbackPlans(selectedCourse);
                diagnostics.Add("Clinical rejected-ID fallback plans: "
                    + FormatIds(fallback.Select(plan => plan.Id)));
                if (fallback.Count == 0)
                {
                    return CourseFailure(selectedCourse, PlanDiscoveryStatus.Missing, method,
                        "Selected planning course has no reviewed plan and no plan whose ID exactly matches an x-prefixed rejected plan after removing the leading x.",
                        diagnostics);
                }

                if (fallback.Count > 1)
                {
                    return CourseFailure(selectedCourse, PlanDiscoveryStatus.Ambiguous, method,
                        "Clinical rejected-plan ID fallback matched multiple plans.", diagnostics);
                }

                reviewed = fallback;
                method = method == PlanDiscoveryMethod.ExactCourseOverride
                    ? PlanDiscoveryMethod.ExactCourseOverrideWithRejectedPlanIdFallback
                    : PlanDiscoveryMethod.RejectedPlanIdFallback;
            }

            if (reviewed.Count > 1)
            {
                return CourseFailure(selectedCourse, PlanDiscoveryStatus.Ambiguous, method,
                    "Selected planning course has multiple reviewed external plans.", diagnostics);
            }

            ExternalPlanDiscoverySnapshot selectedPlan = reviewed[0];
            IsocentreValidationResult isocentre = ValidateSingleIsocentre(
                selectedPlan.TreatmentIsocentres,
                DistinctIsocentreToleranceMm);
            diagnostics.Add("Clinical selected: " + selectedCourse.Id + " / " + selectedPlan.Id);
            diagnostics.Add("Clinical isocentre: " + (isocentre.IsValid
                ? FormatPoint(isocentre.Isocentre)
                : isocentre.Reason));
            return new PlanBranchDiscoveryResult(
                isocentre.IsValid ? PlanDiscoveryStatus.Selected : PlanDiscoveryStatus.Unsupported,
                method,
                selectedCourse.Id,
                selectedPlan.Id,
                isocentre.IsValid ? null : isocentre.Reason,
                diagnostics,
                isocentre);
        }

        private static PlanBranchDiscoveryResult DiscoverPhysics(
            PatientDiscoverySnapshot patient,
            string courseOverride,
            string planOverride,
            CourseDiscoveryConfiguration configuration)
        {
            var diagnostics = new List<string>();
            var coursePattern = new Regex(
                configuration.PhysicsCourseTokenPattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var planPattern = new Regex(
                configuration.PhysicsPlanTokenPattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            List<CourseDiscoverySnapshot> tokenCourses = patient.Courses
                .Where(course => coursePattern.IsMatch(course.Id))
                .ToList();
            diagnostics.Add("Physics token courses: " + FormatIds(tokenCourses.Select(course => course.Id)));
            bool similarCourseFallback = false;
            if (tokenCourses.Count == 0)
            {
                tokenCourses = patient.Courses
                    .Where(course => ContainsSimilarPhysicsToken(course.Id))
                    .ToList();
                similarCourseFallback = tokenCourses.Count > 0;
                diagnostics.Add("Physics similar-token course fallback: "
                    + FormatIds(tokenCourses.Select(course => course.Id)));
            }

            CourseDiscoverySnapshot selectedCourse;
            bool courseWasOverridden = courseOverride != null;
            if (courseWasOverridden)
            {
                List<CourseDiscoverySnapshot> exact = patient.Courses
                    .Where(course => string.Equals(course.Id, courseOverride, StringComparison.Ordinal))
                    .ToList();
                if (exact.Count == 0)
                {
                    return Failure(PlanDiscoveryStatus.Missing,
                        "Exact physics-course override was not found: " + courseOverride,
                        diagnostics);
                }

                if (exact.Count > 1)
                {
                    return Failure(PlanDiscoveryStatus.Ambiguous,
                        "Exact physics-course override matched multiple courses: " + courseOverride,
                        diagnostics);
                }

                selectedCourse = exact[0];
            }
            else
            {
                List<CourseDiscoverySnapshot> candidates = tokenCourses;
                if (candidates.Count > 1 && planOverride != null)
                {
                    candidates = candidates.Where(course => course.ExternalPlans.Any(plan =>
                        string.Equals(plan.Id, planOverride, StringComparison.Ordinal))).ToList();
                    diagnostics.Add("Physics courses after exact plan override: "
                        + FormatIds(candidates.Select(course => course.Id)));
                }

                if (candidates.Count == 0)
                {
                    return Failure(PlanDiscoveryStatus.Missing,
                        "No course ID contains a complete configured PPHYS/PHYS token or a unique delimited token one edit away.", diagnostics);
                }

                if (candidates.Count > 1)
                {
                    return Failure(PlanDiscoveryStatus.Ambiguous,
                        similarCourseFallback
                            ? "Multiple courses contain a similar PPHYS/PHYS token one edit away."
                            : "Multiple courses contain a complete configured PPHYS/PHYS token.",
                        diagnostics);
                }

                selectedCourse = candidates[0];
            }

            diagnostics.Add("Physics selected course: " + selectedCourse.Id);
            List<ExternalPlanDiscoverySnapshot> planCandidates;
            bool similarPlanFallback = false;
            if (planOverride != null)
            {
                planCandidates = selectedCourse.ExternalPlans.Where(plan => string.Equals(
                    plan.Id,
                    planOverride,
                    StringComparison.Ordinal)).ToList();
            }
            else
            {
                planCandidates = selectedCourse.ExternalPlans
                    .Where(plan => planPattern.IsMatch(plan.Id))
                    .ToList();
                if (planCandidates.Count == 0)
                {
                    planCandidates = selectedCourse.ExternalPlans
                        .Where(plan => ContainsSimilarPhysicsToken(plan.Id))
                        .ToList();
                    if (planCandidates.Count > 1)
                    {
                        List<ExternalPlanDiscoverySnapshot> planningApproved = planCandidates
                            .Where(plan => plan.Approval == PlanApprovalKind.PlanningApproved)
                            .ToList();
                        if (planningApproved.Count == 1)
                        {
                            diagnostics.Add("Physics similar-token fallback was disambiguated by unique PlanningApproved status.");
                            planCandidates = planningApproved;
                        }
                    }

                    if (planCandidates.Count > 0)
                    {
                        similarPlanFallback = true;
                    }
                }
            }

            diagnostics.Add("Physics candidate plans: "
                + FormatIds(planCandidates.Select(plan => plan.Id)));
            if (planCandidates.Count == 0)
            {
                return CourseFailure(selectedCourse, PlanDiscoveryStatus.Missing,
                    courseWasOverridden
                        ? PlanDiscoveryMethod.ExactCourseOverride
                        : PlanDiscoveryMethod.Automatic,
                    planOverride == null
                        ? "No external plan ID contains a complete configured PPHYS/PHYS token or a delimited token one edit away."
                        : "Exact physics-plan override was not found in the selected course: " + planOverride,
                    diagnostics);
            }

            if (planCandidates.Count > 1)
            {
                return CourseFailure(selectedCourse, PlanDiscoveryStatus.Ambiguous,
                    courseWasOverridden
                        ? PlanDiscoveryMethod.ExactCourseOverride
                        : PlanDiscoveryMethod.Automatic,
                    planOverride == null
                        ? similarPlanFallback
                            ? "Multiple external plans contain a similar PPHYS/PHYS token one edit away."
                            : "Multiple external plans contain a complete configured PPHYS/PHYS token."
                        : "Exact physics-plan override matched multiple plans: " + planOverride,
                    diagnostics);
            }

            ExternalPlanDiscoverySnapshot selectedPlan = planCandidates[0];
            PlanDiscoveryMethod method = courseWasOverridden && planOverride != null
                ? PlanDiscoveryMethod.ExactCourseAndPlanOverride
                : courseWasOverridden
                    ? PlanDiscoveryMethod.ExactCourseOverride
                        : planOverride != null
                        ? PlanDiscoveryMethod.ExactPlanOverride
                        : similarCourseFallback || similarPlanFallback
                            ? PlanDiscoveryMethod.SimilarPhysicsTokenFallback
                            : PlanDiscoveryMethod.Automatic;
            diagnostics.Add("Physics selected: " + selectedCourse.Id + " / " + selectedPlan.Id);
            return new PlanBranchDiscoveryResult(
                PlanDiscoveryStatus.Selected,
                method,
                selectedCourse.Id,
                selectedPlan.Id,
                null,
                diagnostics,
                null);
        }

        public static IsocentreValidationResult ValidateSingleIsocentre(
            IEnumerable<DiscoveryPoint3D> points,
            double toleranceMm)
        {
            if (points == null)
            {
                throw new ArgumentNullException("points");
            }

            if (double.IsNaN(toleranceMm) || double.IsInfinity(toleranceMm) || toleranceMm < 0.0)
            {
                throw new ArgumentOutOfRangeException("toleranceMm");
            }

            List<DiscoveryPoint3D> ordered = points
                .OrderBy(point => point.X)
                .ThenBy(point => point.Y)
                .ThenBy(point => point.Z)
                .ToList();
            if (ordered.Count == 0)
            {
                return IsocentreValidationResult.Invalid(
                    "Reviewed plan has no non-setup treatment-beam isocentre.", 0);
            }

            for (int first = 0; first < ordered.Count; first++)
            {
                for (int second = first + 1; second < ordered.Count; second++)
                {
                    if (Distance(ordered[first], ordered[second]) > toleranceMm)
                    {
                        return IsocentreValidationResult.Invalid(
                            "Reviewed plan has multiple distinct treatment-beam isocentres.",
                            ordered.Count);
                    }
                }
            }

            return IsocentreValidationResult.Valid(
                new DiscoveryPoint3D(
                    ordered.Average(point => point.X),
                    ordered.Average(point => point.Y),
                    ordered.Average(point => point.Z)),
                ordered.Count);
        }

        private static int RejectedCount(CourseDiscoverySnapshot course)
        {
            return course.ExternalPlans.Count(plan => plan.Approval == PlanApprovalKind.Rejected);
        }

        private static int ReviewedCount(CourseDiscoverySnapshot course)
        {
            return course.ExternalPlans.Count(plan => plan.Approval == PlanApprovalKind.Reviewed);
        }

        private static List<ExternalPlanDiscoverySnapshot> FindRejectedIdFallbackPlans(
            CourseDiscoverySnapshot course)
        {
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ExternalPlanDiscoverySnapshot rejected in course.ExternalPlans.Where(
                plan => plan.Approval == PlanApprovalKind.Rejected))
            {
                string id = rejected.Id.Trim();
                if (id.Length > 1 && (id[0] == 'x' || id[0] == 'X'))
                {
                    targets.Add(id.Substring(1).TrimStart());
                }
            }

            return course.ExternalPlans
                .Where(plan => plan.Approval != PlanApprovalKind.Rejected)
                .Where(plan => targets.Contains(plan.Id.Trim()))
                .OrderBy(plan => plan.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(plan => plan.Id, StringComparer.Ordinal)
                .ToList();
        }

        private static bool ContainsSimilarPhysicsToken(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return Regex.Split(id.Trim(), "[ _-]+")
                .Any(token => IsOneEditAway(token, "PPHYS")
                    || IsOneEditAway(token, "PHYS"));
        }

        private static bool IsOneEditAway(string value, string target)
        {
            if (string.IsNullOrEmpty(value)
                || Math.Abs(value.Length - target.Length) > 1
                || string.Equals(value, target, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            value = value.ToUpperInvariant();
            target = target.ToUpperInvariant();
            var distance = new int[value.Length + 1, target.Length + 1];
            for (int row = 0; row <= value.Length; row++)
            {
                distance[row, 0] = row;
            }

            for (int column = 0; column <= target.Length; column++)
            {
                distance[0, column] = column;
            }

            for (int row = 1; row <= value.Length; row++)
            {
                for (int column = 1; column <= target.Length; column++)
                {
                    int substitution = value[row - 1] == target[column - 1] ? 0 : 1;
                    distance[row, column] = Math.Min(
                        Math.Min(
                            distance[row - 1, column] + 1,
                            distance[row, column - 1] + 1),
                        distance[row - 1, column - 1] + substitution);
                }
            }

            return distance[value.Length, target.Length] == 1;
        }

        private static string FormatCourseSignature(CourseDiscoverySnapshot course)
        {
            return "Planning candidate " + course.Id
                + ": rejected=" + RejectedCount(course).ToString(CultureInfo.InvariantCulture)
                + ", reviewed=" + ReviewedCount(course).ToString(CultureInfo.InvariantCulture)
                + ", planningApproved=" + course.ExternalPlans.Count(
                    plan => plan.Approval == PlanApprovalKind.PlanningApproved)
                    .ToString(CultureInfo.InvariantCulture)
                + ", externalPlans=" + course.ExternalPlans.Count.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatIds(IEnumerable<string> ids)
        {
            string[] ordered = ids.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            return ordered.Length == 0 ? "<none>" : string.Join(" | ", ordered);
        }

        private static string FormatPoint(DiscoveryPoint3D point)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "({0:G17}, {1:G17}, {2:G17}) mm", point.X, point.Y, point.Z);
        }

        private static double Distance(DiscoveryPoint3D first, DiscoveryPoint3D second)
        {
            double x = first.X - second.X;
            double y = first.Y - second.Y;
            double z = first.Z - second.Z;
            return Math.Sqrt((x * x) + (y * y) + (z * z));
        }

        private static PlanBranchDiscoveryResult Failure(
            PlanDiscoveryStatus status,
            string reason,
            IEnumerable<string> diagnostics)
        {
            return new PlanBranchDiscoveryResult(
                status, PlanDiscoveryMethod.None, null, null, reason, diagnostics, null);
        }

        private static PlanBranchDiscoveryResult CourseFailure(
            CourseDiscoverySnapshot course,
            PlanDiscoveryStatus status,
            PlanDiscoveryMethod method,
            string reason,
            IEnumerable<string> diagnostics)
        {
            return new PlanBranchDiscoveryResult(
                status, method, course.Id, null, reason, diagnostics, null);
        }
    }
}
