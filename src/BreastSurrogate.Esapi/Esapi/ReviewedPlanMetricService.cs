using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Esapi
{
    public sealed class ReviewedPlanMetricRequest
    {
        public ReviewedPlanMetricRequest(string structureRole, DvhMetricRequest metric)
        {
            if (!string.Equals(structureRole, "IpsilateralLung", StringComparison.Ordinal)
                && !string.Equals(structureRole, "Heart", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Structure role must be IpsilateralLung or Heart.",
                    "structureRole");
            }

            if (metric == null)
            {
                throw new ArgumentNullException("metric");
            }
            StructureRole = structureRole;
            Metric = metric;
        }

        public string StructureRole { get; private set; }
        public DvhMetricRequest Metric { get; private set; }
    }

    public sealed class ReviewedPlanMetricResult
    {
        public ReviewedPlanMetricResult(
            string planId,
            int? fractions,
            string lungStructureId,
            string heartStructureId,
            IEnumerable<MetricCalculationResult> metrics)
        {
            PlanId = planId;
            Fractions = fractions;
            LungStructureId = lungStructureId;
            HeartStructureId = heartStructureId;
            Metrics = new List<MetricCalculationResult>(metrics).AsReadOnly();
        }

        public string PlanId { get; private set; }
        public int? Fractions { get; private set; }
        public string LungStructureId { get; private set; }
        public string HeartStructureId { get; private set; }
        public IList<MetricCalculationResult> Metrics { get; private set; }
    }

    /// <summary>
    /// Resolves structures only from the supplied reviewed plan's structure set
    /// and evaluates requested clinical DVH metrics read-only.
    /// </summary>
    public sealed class ReviewedPlanMetricService
    {
        public ReviewedPlanMetricResult Calculate(
            PlanSetup reviewedPlan,
            VVector reviewedPlanIsocentre,
            IEnumerable<ReviewedPlanMetricRequest> requests,
            double binWidthGy)
        {
            if (reviewedPlan == null)
            {
                throw new ArgumentNullException("reviewedPlan");
            }

            if (requests == null)
            {
                throw new ArgumentNullException("requests");
            }

            List<ReviewedPlanMetricRequest> requestList = requests.ToList();
            if (requestList.Any(request => request == null))
            {
                throw new ArgumentException("Metric requests cannot contain null.", "requests");
            }

            if (reviewedPlan.StructureSet == null)
            {
                return UnavailablePlan(
                    reviewedPlan,
                    requestList,
                    "The reviewed plan has no structure set.");
            }

            List<Structure> structures = reviewedPlan.StructureSet.Structures
                .Where(structure => structure != null)
                .ToList();
            Structure lung = null;
            IpsilateralLungSelectionException lungFailure = null;
            if (requestList.Any(request => request.StructureRole == "IpsilateralLung"))
            {
                try
                {
                    lung = new IpsilateralLungSelector()
                        .Select(structures, reviewedPlanIsocentre)
                        .SelectedStructure;
                }
                catch (IpsilateralLungSelectionException exception)
                {
                    lungFailure = exception;
                }
            }

            EsapiStructureSelectionResult heartSelection = null;
            if (requestList.Any(request => request.StructureRole == "Heart"))
            {
                heartSelection = new HeartStructureSelector().Select(structures);
            }

            var evaluator = new DvhMetricEvaluator();
            var results = new List<MetricCalculationResult>();
            foreach (ReviewedPlanMetricRequest request in requestList)
            {
                Structure structure = request.StructureRole == "IpsilateralLung"
                    ? lung
                    : heartSelection != null && heartSelection.IsSelected
                        ? heartSelection.SelectedStructure
                        : null;
                if (structure != null)
                {
                    results.Add(evaluator.Evaluate(
                        new EsapiDvhDataSource(reviewedPlan, structure),
                        request.Metric,
                        binWidthGy));
                    continue;
                }

                if (request.StructureRole == "IpsilateralLung")
                {
                    results.Add(MetricCalculationResult.Unavailable(
                        request.Metric.Name,
                        null,
                        LegacyStructureMetricService.MapLungFailure(lungFailure),
                        lungFailure == null
                            ? "The reviewed-plan ipsilateral lung is unavailable."
                            : lungFailure.Message));
                }
                else
                {
                    results.Add(MetricCalculationResult.Unavailable(
                        request.Metric.Name,
                        null,
                        LegacyStructureMetricService.MapIdSelectionFailure(
                            heartSelection.Diagnostics),
                        heartSelection.Diagnostics.FailureReason));
                }
            }

            return new ReviewedPlanMetricResult(
                reviewedPlan.Id,
                reviewedPlan.NumberOfFractions,
                lung == null ? null : lung.Id,
                heartSelection != null && heartSelection.IsSelected
                    ? heartSelection.SelectedStructure.Id
                    : null,
                results);
        }

        private static ReviewedPlanMetricResult UnavailablePlan(
            PlanSetup reviewedPlan,
            IEnumerable<ReviewedPlanMetricRequest> requests,
            string reason)
        {
            return new ReviewedPlanMetricResult(
                reviewedPlan.Id,
                reviewedPlan.NumberOfFractions,
                null,
                null,
                requests.Select(request => MetricCalculationResult.Unavailable(
                    request.Metric.Name,
                    null,
                    MetricCalculationStatus.MissingData,
                    reason)));
        }
    }
}
