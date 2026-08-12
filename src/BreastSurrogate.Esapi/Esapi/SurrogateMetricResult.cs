using System;

namespace BreastSurrogate.Esapi.Esapi
{
    public enum SurrogateMetricStatus
    {
        Available,
        Unavailable
    }

    /// <summary>
    /// Immutable structured outcome for one geometric surrogate metric.
    /// Contains no persistent ESAPI object.
    /// </summary>
    public sealed class SurrogateMetricResult
    {
        private SurrogateMetricResult(
            string metricName,
            string structureId,
            SurrogateMetricStatus status,
            double? value,
            string unit,
            string failureReason,
            MetricCalculationStatus failureStatus,
            StructureVoxelSamplingResult samplingResult)
        {
            if (string.IsNullOrWhiteSpace(metricName))
            {
                throw new ArgumentException("Metric name cannot be null or empty.", "metricName");
            }

            MetricName = metricName;
            StructureId = structureId;
            Status = status;
            Value = value;
            Unit = unit;
            FailureReason = failureReason;
            FailureStatus = failureStatus;
            SamplingResult = samplingResult;
        }

        public string MetricName { get; private set; }

        public string StructureId { get; private set; }

        public SurrogateMetricStatus Status { get; private set; }

        public bool IsAvailable
        {
            get { return Status == SurrogateMetricStatus.Available; }
        }

        public double? Value { get; private set; }

        public string Unit { get; private set; }

        public string FailureReason { get; private set; }

        public MetricCalculationStatus FailureStatus { get; private set; }

        public StructureVoxelSamplingResult SamplingResult { get; private set; }

        public static SurrogateMetricResult Available(
            string metricName,
            StructureVoxelSamplingResult samplingResult)
        {
            if (samplingResult == null)
            {
                throw new ArgumentNullException("samplingResult");
            }

            double value = samplingResult
                .InFieldResult
                .EitherFieldPercentageOfEsapiVolume;
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentException(
                    "An available metric must have a finite value.",
                    "samplingResult");
            }

            return new SurrogateMetricResult(
                metricName,
                samplingResult.StructureId,
                SurrogateMetricStatus.Available,
                value,
                "%",
                null,
                MetricCalculationStatus.Available,
                samplingResult);
        }

        public static SurrogateMetricResult Unavailable(
            string metricName,
            string structureId,
            string failureReason)
        {
            return Unavailable(
                metricName,
                structureId,
                MetricCalculationStatus.CalculationFailed,
                failureReason);
        }

        public static SurrogateMetricResult Unavailable(
            string metricName,
            string structureId,
            MetricCalculationStatus failureStatus,
            string failureReason)
        {
            if (failureStatus == MetricCalculationStatus.Available)
            {
                throw new ArgumentException(
                    "An unavailable metric cannot have Available failure status.",
                    "failureStatus");
            }

            if (string.IsNullOrWhiteSpace(failureReason))
            {
                throw new ArgumentException(
                    "An unavailable metric must have a failure reason.",
                    "failureReason");
            }

            return new SurrogateMetricResult(
                metricName,
                structureId,
                SurrogateMetricStatus.Unavailable,
                null,
                null,
                failureReason,
                failureStatus,
                null);
        }
    }
}
