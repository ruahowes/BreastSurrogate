using System;

namespace BreastSurrogate.Esapi.Esapi
{
    public enum MetricCalculationStatus
    {
        Available,
        MissingData,
        Ambiguous,
        Unsupported,
        CalculationFailed
    }

    /// <summary>
    /// Immutable scalar metric outcome containing no persistent ESAPI object.
    /// </summary>
    public sealed class MetricCalculationResult
    {
        private MetricCalculationResult(
            string metricName,
            string structureId,
            MetricCalculationStatus status,
            double? value,
            string unit,
            string reason,
            string nativeDoseUnit)
        {
            if (string.IsNullOrWhiteSpace(metricName))
            {
                throw new ArgumentException("Metric name is required.", "metricName");
            }

            MetricName = metricName;
            StructureId = structureId;
            Status = status;
            Value = value;
            Unit = unit;
            Reason = reason;
            NativeDoseUnit = nativeDoseUnit;
        }

        public string MetricName { get; private set; }
        public string StructureId { get; private set; }
        public MetricCalculationStatus Status { get; private set; }
        public double? Value { get; private set; }
        public string Unit { get; private set; }
        public string Reason { get; private set; }
        public string NativeDoseUnit { get; private set; }
        public bool IsAvailable { get { return Status == MetricCalculationStatus.Available; } }

        public static MetricCalculationResult Available(
            string metricName,
            string structureId,
            double value,
            string unit,
            string nativeDoseUnit)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException("value", "Metric value must be finite.");
            }

            if (string.IsNullOrWhiteSpace(unit))
            {
                throw new ArgumentException("Metric unit is required.", "unit");
            }

            return new MetricCalculationResult(
                metricName,
                structureId,
                MetricCalculationStatus.Available,
                value,
                unit,
                null,
                nativeDoseUnit);
        }

        public static MetricCalculationResult Unavailable(
            string metricName,
            string structureId,
            MetricCalculationStatus status,
            string reason)
        {
            if (status == MetricCalculationStatus.Available)
            {
                throw new ArgumentException("Unavailable status cannot be Available.", "status");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Unavailable metric reason is required.", "reason");
            }

            return new MetricCalculationResult(
                metricName,
                structureId,
                status,
                null,
                null,
                reason,
                null);
        }
    }
}
