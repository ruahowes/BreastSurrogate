using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Esapi
{
    public sealed class LegacyPlanMetricResult
    {
        public LegacyPlanMetricResult(
            MetricCalculationResult ilf,
            MetricCalculationResult hif,
            string lungStructureId,
            string heartStructureId,
            string ilfStructureId,
            string hifStructureId)
        {
            if (ilf == null)
            {
                throw new ArgumentNullException("ilf");
            }

            if (hif == null)
            {
                throw new ArgumentNullException("hif");
            }

            Ilf = ilf;
            Hif = hif;
            LungStructureId = lungStructureId;
            HeartStructureId = heartStructureId;
            IlfStructureId = ilfStructureId;
            HifStructureId = hifStructureId;
        }

        public MetricCalculationResult Ilf { get; private set; }
        public MetricCalculationResult Hif { get; private set; }
        public string LungStructureId { get; private set; }
        public string HeartStructureId { get; private set; }
        public string IlfStructureId { get; private set; }
        public string HifStructureId { get; private set; }
    }

    /// <summary>
    /// Selects and evaluates legacy ILF/HIF structures from one physics-plan
    /// structure set. No selected Structure escapes the call.
    /// </summary>
    public sealed class LegacyStructureMetricService
    {
        public LegacyPlanMetricResult Calculate(
            IEnumerable<Structure> structures,
            VVector? referenceIsocentre)
        {
            if (structures == null)
            {
                throw new ArgumentNullException("structures");
            }

            List<Structure> structureList = structures
                .Where(structure => structure != null)
                .ToList();
            var legacySelector = new LegacySurrogateStructureSelector();
            EsapiStructureSelectionResult ilfSelection = legacySelector.Select(
                structureList,
                LegacySurrogateStructureKind.Ilf);
            EsapiStructureSelectionResult hifSelection = legacySelector.Select(
                structureList,
                LegacySurrogateStructureKind.Hif);

            IpsilateralLungSelectionResult lungSelection = null;
            IpsilateralLungSelectionException lungFailure = null;
            if (!referenceIsocentre.HasValue)
            {
                lungFailure = new IpsilateralLungSelectionException(
                    "A unique ANT MED treatment-beam isocentre is required for ipsilateral-lung selection.",
                    IpsilateralLungSelectionFailureKind.MissingData);
            }
            else
            {
                try
                {
                    lungSelection = new IpsilateralLungSelector().Select(
                        structureList,
                        referenceIsocentre.Value);
                }
                catch (IpsilateralLungSelectionException exception)
                {
                    lungFailure = exception;
                }
            }

            EsapiStructureSelectionResult heartSelection =
                new HeartStructureSelector().Select(structureList);

            MetricCalculationResult ilf = CalculateIlf(
                ilfSelection,
                lungSelection,
                lungFailure);
            MetricCalculationResult hif = CalculateHif(
                hifSelection,
                heartSelection);
            return new LegacyPlanMetricResult(
                ilf,
                hif,
                lungSelection == null ? null : lungSelection.SelectedStructure.Id,
                heartSelection.IsSelected ? heartSelection.SelectedStructure.Id : null,
                ilfSelection.IsSelected ? ilfSelection.SelectedStructure.Id : null,
                hifSelection.IsSelected ? hifSelection.SelectedStructure.Id : null);
        }

        private static MetricCalculationResult CalculateIlf(
            EsapiStructureSelectionResult numerator,
            IpsilateralLungSelectionResult denominator,
            IpsilateralLungSelectionException denominatorFailure)
        {
            if (!numerator.IsSelected)
            {
                return SelectionUnavailable("ILF", numerator.Diagnostics);
            }

            if (denominator == null)
            {
                return MetricCalculationResult.Unavailable(
                    "ILF",
                    numerator.SelectedStructure.Id,
                    MapLungFailure(denominatorFailure),
                    denominatorFailure == null
                        ? "The ipsilateral-lung denominator is unavailable."
                        : denominatorFailure.Message);
            }

            try
            {
                return LegacyVolumeRatioEvaluator.Evaluate(
                    "ILF",
                    numerator.SelectedStructure.Id,
                    numerator.SelectedStructure.Volume,
                    denominator.SelectedStructure.Id,
                    denominator.SelectedStructure.Volume);
            }
            catch (Exception exception)
            {
                return QueryFailure("ILF", numerator.SelectedStructure.Id, exception);
            }
        }

        private static MetricCalculationResult CalculateHif(
            EsapiStructureSelectionResult numerator,
            EsapiStructureSelectionResult denominator)
        {
            if (!numerator.IsSelected)
            {
                return SelectionUnavailable("HIF", numerator.Diagnostics);
            }

            if (!denominator.IsSelected)
            {
                return MetricCalculationResult.Unavailable(
                    "HIF",
                    numerator.SelectedStructure.Id,
                    MapIdSelectionFailure(denominator.Diagnostics),
                    denominator.Diagnostics.FailureReason);
            }

            try
            {
                return LegacyVolumeRatioEvaluator.Evaluate(
                    "HIF",
                    numerator.SelectedStructure.Id,
                    numerator.SelectedStructure.Volume,
                    denominator.SelectedStructure.Id,
                    denominator.SelectedStructure.Volume);
            }
            catch (Exception exception)
            {
                return QueryFailure("HIF", numerator.SelectedStructure.Id, exception);
            }
        }

        private static MetricCalculationResult SelectionUnavailable(
            string metricName,
            StructureIdSelectionResult selection)
        {
            return MetricCalculationResult.Unavailable(
                metricName,
                null,
                MapIdSelectionFailure(selection),
                selection.FailureReason);
        }

        internal static MetricCalculationStatus MapIdSelectionFailure(
            StructureIdSelectionResult selection)
        {
            int usable = selection.Candidates.Count(candidate => candidate.IsUsable);
            return usable > 1
                ? MetricCalculationStatus.Ambiguous
                : MetricCalculationStatus.MissingData;
        }

        internal static MetricCalculationStatus MapLungFailure(
            IpsilateralLungSelectionException exception)
        {
            if (exception != null
                && exception.FailureKind == IpsilateralLungSelectionFailureKind.Ambiguous)
            {
                return MetricCalculationStatus.Ambiguous;
            }

            return exception != null
                && exception.FailureKind == IpsilateralLungSelectionFailureKind.InvalidData
                ? MetricCalculationStatus.CalculationFailed
                : MetricCalculationStatus.MissingData;
        }

        private static MetricCalculationResult QueryFailure(
            string metricName,
            string structureId,
            Exception exception)
        {
            return MetricCalculationResult.Unavailable(
                metricName,
                structureId,
                MetricCalculationStatus.CalculationFailed,
                "Could not read structure volumes: " + exception.Message);
        }
    }
}
