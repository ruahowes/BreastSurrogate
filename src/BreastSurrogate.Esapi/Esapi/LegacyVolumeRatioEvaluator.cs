using System;

namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// Pure evaluator for already-created legacy intersection structure volumes.
    /// </summary>
    public static class LegacyVolumeRatioEvaluator
    {
        public static MetricCalculationResult Evaluate(
            string metricName,
            string numeratorStructureId,
            double numeratorVolumeCm3,
            string denominatorStructureId,
            double denominatorVolumeCm3)
        {
            if (string.IsNullOrWhiteSpace(numeratorStructureId))
            {
                return MetricCalculationResult.Unavailable(
                    metricName,
                    null,
                    MetricCalculationStatus.MissingData,
                    "The legacy numerator structure is unavailable.");
            }

            if (string.IsNullOrWhiteSpace(denominatorStructureId))
            {
                return MetricCalculationResult.Unavailable(
                    metricName,
                    numeratorStructureId,
                    MetricCalculationStatus.MissingData,
                    "The denominator structure is unavailable.");
            }

            if (!IsFinite(numeratorVolumeCm3) || numeratorVolumeCm3 < 0.0)
            {
                return MetricCalculationResult.Unavailable(
                    metricName,
                    numeratorStructureId,
                    MetricCalculationStatus.CalculationFailed,
                    "The legacy numerator structure volume is invalid.");
            }

            if (!IsFinite(denominatorVolumeCm3) || denominatorVolumeCm3 <= 0.0)
            {
                return MetricCalculationResult.Unavailable(
                    metricName,
                    numeratorStructureId,
                    MetricCalculationStatus.CalculationFailed,
                    "The denominator structure volume must be finite and greater than zero.");
            }

            double value = 100.0 * numeratorVolumeCm3 / denominatorVolumeCm3;
            if (!IsFinite(value))
            {
                return MetricCalculationResult.Unavailable(
                    metricName,
                    numeratorStructureId,
                    MetricCalculationStatus.CalculationFailed,
                    "The legacy volume ratio is not finite.");
            }

            return MetricCalculationResult.Available(
                metricName,
                numeratorStructureId,
                value,
                "%",
                null);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
