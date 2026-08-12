using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BreastSurrogate.Esapi.Esapi;

namespace BreastSurrogate.Batch
{
    public interface IPatientAuditSession : IDisposable
    {
        PatientDiscoverySnapshot CreateDiscoverySnapshot();
        PhysicsPlanMetricResult CalculatePhysics(string courseId, string planId);
        ReviewedPlanMetricResult CalculateClinical(
            string courseId,
            string planId,
            DiscoveryPoint3D isocentre,
            IList<ReviewedPlanMetricRequest> requests,
            double binWidthGy);
    }

    public interface IPatientAuditSource
    {
        IPatientAuditSession OpenPatient(string patientId);
    }

    public sealed class PatientNotFoundException : Exception
    {
        public PatientNotFoundException(string patientId)
            : base("Patient was not found: " + patientId)
        {
        }
    }

    public sealed class PatientAuditResult
    {
        private readonly List<string> _logLines;

        internal PatientAuditResult(
            BatchOutputRow row,
            IEnumerable<string> logLines,
            bool hasFailures)
        {
            Row = row;
            _logLines = new List<string>(logLines);
            HasFailures = hasFailures;
        }

        public BatchOutputRow Row { get; private set; }
        public IList<string> LogLines { get { return _logLines.AsReadOnly(); } }
        public bool HasFailures { get; private set; }

        internal void AddWarning(string warning)
        {
            string existing = Row.GetValue("Warnings");
            Row.SetText(
                "Warnings",
                string.IsNullOrWhiteSpace(existing) ? warning : existing + " | " + warning);
            _logLines.Add("Warning: " + warning);
            HasFailures = true;
        }

        internal void AddLogLine(string line)
        {
            _logLines.Add(line);
        }
    }

    public sealed class PatientAuditProcessor
    {
        public PatientAuditResult Process(
            PatientInputRow input,
            BatchConfiguration configuration,
            string applicationVersion,
            IPatientAuditSession session)
        {
            if (input == null) throw new ArgumentNullException("input");
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (session == null) throw new ArgumentNullException("session");

            BatchOutputRow row = CreateBaseRow(input, configuration, applicationVersion);
            var log = new List<string>
            {
                "PatientId: " + input.PatientId,
                "Input CSV line: " + input.LineNumber.ToString(CultureInfo.InvariantCulture)
            };
            PatientPlanDiscoveryResult discovery = PlanDiscoveryService.Discover(
                session.CreateDiscoverySnapshot(),
                input,
                configuration.CourseDiscovery);
            PopulateDiscovery(row, discovery, log);

            bool hasFailures = false;
            if (discovery.Physics.IsSelected)
            {
                try
                {
                    PopulatePhysics(
                        row,
                        session.CalculatePhysics(
                            discovery.Physics.CourseId,
                            discovery.Physics.PlanId),
                        log);
                }
                catch (Exception exception)
                {
                    hasFailures = true;
                    SetPhysicsUnavailable(
                        row,
                        AuditValueStatus.CalculationFailed,
                        "Physics calculations failed: " + exception.Message);
                    log.Add("Physics calculation exception: "
                        + exception.GetType().FullName + ": " + exception.Message);
                }
            }
            else
            {
                hasFailures = true;
                SetPhysicsUnavailable(
                    row,
                    MapDiscoveryStatus(discovery.Physics.Status),
                    discovery.Physics.Reason);
            }

            if (discovery.Clinical.IsSelected)
            {
                try
                {
                    PopulateClinical(
                        row,
                        session.CalculateClinical(
                            discovery.Clinical.CourseId,
                            discovery.Clinical.PlanId,
                            discovery.Clinical.IsocentreValidation.Isocentre,
                            AuditDvhMetricRequestMapper.Create(configuration.Metrics),
                            configuration.DvhBinWidthGy),
                        configuration,
                        log);
                }
                catch (Exception exception)
                {
                    hasFailures = true;
                    SetClinicalUnavailable(
                        row,
                        configuration,
                        AuditValueStatus.CalculationFailed,
                        "Clinical calculations failed: " + exception.Message);
                    log.Add("Clinical calculation exception: "
                        + exception.GetType().FullName + ": " + exception.Message);
                }
            }
            else
            {
                hasFailures = true;
                SetClinicalUnavailable(
                    row,
                    configuration,
                    MapDiscoveryStatus(discovery.Clinical.Status),
                    discovery.Clinical.Reason);
            }

            hasFailures = hasFailures || HasUnavailableMetrics(row, configuration);
            AddMetricLogLines(row, configuration, log);
            return new PatientAuditResult(row, log, hasFailures);
        }

        public PatientAuditResult CreateFailure(
            PatientInputRow input,
            BatchConfiguration configuration,
            string applicationVersion,
            AuditValueStatus status,
            string reason)
        {
            BatchOutputRow row = CreateBaseRow(input, configuration, applicationVersion);
            SetPhysicsUnavailable(row, status, reason);
            SetClinicalUnavailable(row, configuration, status, reason);
            row.SetText("DiscoveryFailures", reason);
            return new PatientAuditResult(
                row,
                new[]
                {
                    "PatientId: " + input.PatientId,
                    "Input CSV line: " + input.LineNumber.ToString(CultureInfo.InvariantCulture),
                    "Patient processing failure: " + reason
                },
                true);
        }

        private static BatchOutputRow CreateBaseRow(
            PatientInputRow input,
            BatchConfiguration configuration,
            string applicationVersion)
        {
            var row = new BatchOutputRow();
            row.SetText("PatientId", input.PatientId);
            row.SetText("RequestedPlanningCourseId", input.PlanningCourseId);
            row.SetText("RequestedPhysicsCourseId", input.PhysicsCourseId);
            row.SetText("RequestedPhysicsPlanId", input.PhysicsPlanId);
            row.SetText("ConfigurationVersion", configuration.Version.ToString(CultureInfo.InvariantCulture));
            row.SetText("ConfigurationHash", configuration.Hash);
            row.SetText("ApplicationVersion", applicationVersion);
            row.SetText("Warnings", null);
            row.SetText("DiscoveryFailures", null);
            SetPhysicsUnavailable(row, AuditValueStatus.NotCalculated, "Not calculated.");
            SetClinicalUnavailable(
                row,
                configuration,
                AuditValueStatus.NotCalculated,
                "Not calculated.");
            return row;
        }

        private static void PopulateDiscovery(
            BatchOutputRow row,
            PatientPlanDiscoveryResult discovery,
            ICollection<string> log)
        {
            PopulateBranchDiscovery(row, "Clinical", discovery.Clinical, log);
            PopulateBranchDiscovery(row, "Physics", discovery.Physics, log);
            row.SetText("ResolvedPlanningCourseId", discovery.Clinical.CourseId);
            row.SetText("ResolvedClinicalPlanId", discovery.Clinical.PlanId);
            row.SetText("ResolvedPhysicsCourseId", discovery.Physics.CourseId);
            row.SetText("ResolvedPhysicsPlanId", discovery.Physics.PlanId);

            IsocentreValidationResult isocentre = discovery.Clinical.IsocentreValidation;
            if (isocentre != null)
            {
                if (isocentre.IsValid)
                {
                    row.SetText("ClinicalIsocentreXmm", Format(isocentre.Isocentre.X));
                    row.SetText("ClinicalIsocentreYmm", Format(isocentre.Isocentre.Y));
                    row.SetText("ClinicalIsocentreZmm", Format(isocentre.Isocentre.Z));
                }

                row.SetText("ClinicalIsocentreReason", isocentre.Reason);
            }

            string failures = string.Join(" | ", new[]
            {
                discovery.Clinical.IsSelected ? null : "Clinical: " + discovery.Clinical.Reason,
                discovery.Physics.IsSelected ? null : "Physics: " + discovery.Physics.Reason
            }.Where(value => value != null));
            row.SetText("DiscoveryFailures", failures);
        }

        private static void PopulateBranchDiscovery(
            BatchOutputRow row,
            string prefix,
            PlanBranchDiscoveryResult branch,
            ICollection<string> log)
        {
            row.SetText(prefix + "DiscoveryStatus", branch.Status.ToString());
            row.SetText(prefix + "DiscoveryMethod", branch.Method.ToString());
            row.SetText(prefix + "DiscoveryReason", branch.Reason);
            log.Add(prefix + " discovery: " + branch.Status + " via " + branch.Method
                + (branch.Reason == null ? string.Empty : " - " + branch.Reason));
            foreach (string diagnostic in branch.Diagnostics)
            {
                log.Add(prefix + " discovery detail: " + diagnostic);
            }
        }

        private static void PopulatePhysics(
            BatchOutputRow row,
            PhysicsPlanMetricResult result,
            ICollection<string> log)
        {
            row.SetMetric("gILF", "ValuePercent", MapSurrogate(
                result.Geometric.GeometricIlf,
                result.Geometric.SharedFailureReason));
            row.SetMetric("gHIF", "ValuePercent", MapSurrogate(
                result.Geometric.GeometricHif,
                result.Geometric.SharedFailureReason));
            row.SetMetric("ILF", "ValuePercent", MapMetric(result.Legacy.Ilf));
            row.SetMetric("HIF", "ValuePercent", MapMetric(result.Legacy.Hif));
            row.SetText("PhysicsIpsilateralLungStructureId", result.Legacy.LungStructureId);
            row.SetText("PhysicsHeartStructureId", result.Legacy.HeartStructureId);
            row.SetText("ILFStructureId", result.Legacy.IlfStructureId);
            row.SetText("HIFStructureId", result.Legacy.HifStructureId);
            log.Add("Physics calculations completed for plan " + result.Geometric.PlanId + ".");
            log.Add("Physics calculation status: " + result.Geometric.Status);
            log.Add("Treatment beam count: "
                + result.Geometric.TreatmentBeamCount.ToString(CultureInfo.InvariantCulture));
            if (result.Geometric.Field1 != null && result.Geometric.Field2 != null)
            {
                log.Add("Selected treatment beams: "
                    + result.Geometric.Field1.BeamId + " | " + result.Geometric.Field2.BeamId);
            }

            if (result.Geometric.SharedFailureReason != null)
            {
                log.Add("Shared geometric failure: " + result.Geometric.SharedFailureReason);
            }

            AddSamplingDiagnostics(result.Geometric.GeometricIlf, log);
            AddSamplingDiagnostics(result.Geometric.GeometricHif, log);
        }

        private static void PopulateClinical(
            BatchOutputRow row,
            ReviewedPlanMetricResult result,
            BatchConfiguration configuration,
            ICollection<string> log)
        {
            row.SetInteger("Fractions", result.Fractions);
            row.SetText("ClinicalIpsilateralLungStructureId", result.LungStructureId);
            row.SetText("ClinicalHeartStructureId", result.HeartStructureId);
            var byName = result.Metrics.ToDictionary(
                metric => metric.MetricName,
                StringComparer.OrdinalIgnoreCase);
            foreach (AuditMetricConfiguration configured in configuration.Metrics)
            {
                MetricCalculationResult metric;
                if (!byName.TryGetValue(configured.Name, out metric))
                {
                    row.SetMetric(
                        configured.Name,
                        "Value",
                        AuditMetricOutput.Unavailable(
                            AuditValueStatus.NotCalculated,
                            "The clinical metric service returned no result."));
                    continue;
                }

                row.SetMetric(configured.Name, "Value", MapMetric(metric));
                if (!string.IsNullOrWhiteSpace(metric.NativeDoseUnit))
                {
                    log.Add(configured.Name + " native ESAPI dose unit: " + metric.NativeDoseUnit);
                }
            }

            log.Add("Clinical calculations completed for plan " + result.PlanId + ".");
        }

        private static void AddSamplingDiagnostics(
            SurrogateMetricResult metric,
            ICollection<string> log)
        {
            if (metric.SamplingResult == null)
            {
                return;
            }

            StructureVoxelSamplingResult sampling = metric.SamplingResult;
            log.Add(metric.MetricName + " structure: " + sampling.StructureId);
            log.Add(metric.MetricName + " candidate voxels: "
                + sampling.CandidateVoxelCount.ToString(CultureInfo.InvariantCulture));
            log.Add(metric.MetricName + " inside-structure voxels: "
                + sampling.InsideStructureVoxelCount.ToString(CultureInfo.InvariantCulture));
            log.Add(metric.MetricName + " membership queries: "
                + sampling.StructureMembershipQueryCount.ToString(CultureInfo.InvariantCulture));
            log.Add(metric.MetricName + " sampled volume cc: "
                + Format(sampling.SampledStructureVolumeCubicCentimetres));
            log.Add(metric.MetricName + " ESAPI structure volume cc: "
                + Format(sampling.EsapiStructureVolumeCubicCentimetres));
            log.Add(metric.MetricName + " elapsed ms: "
                + sampling.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
        }

        private static AuditMetricOutput MapSurrogate(
            SurrogateMetricResult metric,
            string sharedFailureReason)
        {
            if (metric.IsAvailable)
            {
                return AuditMetricOutput.Available(metric.Value.Value, metric.Unit);
            }

            AuditValueStatus status = sharedFailureReason != null
                ? AuditValueStatus.Unsupported
                : MapMetricStatus(metric.FailureStatus);
            return AuditMetricOutput.Unavailable(status, metric.FailureReason);
        }

        private static AuditMetricOutput MapMetric(MetricCalculationResult metric)
        {
            return metric.IsAvailable
                ? AuditMetricOutput.Available(metric.Value.Value, metric.Unit)
                : AuditMetricOutput.Unavailable(MapMetricStatus(metric.Status), metric.Reason);
        }

        private static AuditValueStatus MapMetricStatus(MetricCalculationStatus status)
        {
            switch (status)
            {
                case MetricCalculationStatus.MissingData:
                    return AuditValueStatus.MissingData;
                case MetricCalculationStatus.Ambiguous:
                    return AuditValueStatus.Ambiguous;
                case MetricCalculationStatus.Unsupported:
                    return AuditValueStatus.Unsupported;
                case MetricCalculationStatus.CalculationFailed:
                    return AuditValueStatus.CalculationFailed;
                default:
                    throw new ArgumentOutOfRangeException("status");
            }
        }

        private static AuditValueStatus MapDiscoveryStatus(PlanDiscoveryStatus status)
        {
            switch (status)
            {
                case PlanDiscoveryStatus.Missing:
                    return AuditValueStatus.MissingData;
                case PlanDiscoveryStatus.Ambiguous:
                    return AuditValueStatus.Ambiguous;
                case PlanDiscoveryStatus.Unsupported:
                    return AuditValueStatus.Unsupported;
                default:
                    return AuditValueStatus.NotCalculated;
            }
        }

        private static void SetPhysicsUnavailable(
            BatchOutputRow row,
            AuditValueStatus status,
            string reason)
        {
            foreach (string metric in new[] { "gILF", "gHIF", "ILF", "HIF" })
            {
                row.SetMetric(
                    metric,
                    "ValuePercent",
                    AuditMetricOutput.Unavailable(status, reason));
            }
        }

        private static void SetClinicalUnavailable(
            BatchOutputRow row,
            BatchConfiguration configuration,
            AuditValueStatus status,
            string reason)
        {
            foreach (AuditMetricConfiguration metric in configuration.Metrics)
            {
                row.SetMetric(
                    metric.Name,
                    "Value",
                    AuditMetricOutput.Unavailable(status, reason));
            }
        }

        private static bool HasUnavailableMetrics(
            BatchOutputRow row,
            BatchConfiguration configuration)
        {
            var names = new List<string> { "gILF", "gHIF", "ILF", "HIF" };
            names.AddRange(configuration.Metrics.Select(metric => metric.Name));
            return names.Any(name => !string.Equals(
                row.GetValue(name + "_Status"),
                AuditValueStatus.Available.ToString(),
                StringComparison.Ordinal));
        }

        private static void AddMetricLogLines(
            BatchOutputRow row,
            BatchConfiguration configuration,
            ICollection<string> log)
        {
            var metrics = new List<string> { "gILF", "gHIF", "ILF", "HIF" };
            metrics.AddRange(configuration.Metrics.Select(metric => metric.Name));
            foreach (string metric in metrics)
            {
                string suffix = metric == "gILF" || metric == "gHIF"
                    || metric == "ILF" || metric == "HIF"
                    ? "ValuePercent"
                    : "Value";
                log.Add(metric + ": value=" + row.GetValue(metric + "_" + suffix)
                    + ", unit=" + row.GetValue(metric + "_Unit")
                    + ", status=" + row.GetValue(metric + "_Status")
                    + ", reason=" + row.GetValue(metric + "_Reason"));
            }
        }

        private static string Format(double value)
        {
            return value.ToString("G17", CultureInfo.InvariantCulture);
        }
    }
}
