using System;

namespace BreastSurrogate.Esapi.Esapi
{
    public enum DvhMetricKind
    {
        MeanDose,
        VolumeAtDose,
        DoseAtVolume
    }

    public enum DvhVolumeKind
    {
        None,
        RelativePercent,
        AbsoluteCm3
    }

    public enum DvhValueUnit
    {
        Gy,
        Centigray,
        RelativePercent,
        AbsoluteCm3,
        Unknown
    }

    public sealed class DvhMetricRequest
    {
        public DvhMetricRequest(
            string name,
            DvhMetricKind kind,
            double? doseGy,
            double? volume,
            DvhVolumeKind volumeKind)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Metric name is required.", "name");
            }

            if (kind == DvhMetricKind.MeanDose)
            {
                if (doseGy.HasValue || volume.HasValue || volumeKind != DvhVolumeKind.None)
                {
                    throw new ArgumentException("MeanDose does not accept dose or volume query values.");
                }
            }
            else if (kind == DvhMetricKind.VolumeAtDose)
            {
                ValidateNonNegative(doseGy, "doseGy");
                if (volume.HasValue || !IsQueryVolumeKind(volumeKind))
                {
                    throw new ArgumentException("VolumeAtDose requires doseGy and a volume kind.");
                }
            }
            else if (kind == DvhMetricKind.DoseAtVolume)
            {
                ValidateNonNegative(volume, "volume");
                if (doseGy.HasValue || !IsQueryVolumeKind(volumeKind))
                {
                    throw new ArgumentException("DoseAtVolume requires volume and a volume kind.");
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException("kind");
            }

            Name = name;
            Kind = kind;
            DoseGy = doseGy;
            Volume = volume;
            VolumeKind = volumeKind;
        }

        public string Name { get; private set; }
        public DvhMetricKind Kind { get; private set; }
        public double? DoseGy { get; private set; }
        public double? Volume { get; private set; }
        public DvhVolumeKind VolumeKind { get; private set; }

        private static void ValidateNonNegative(double? value, string parameterName)
        {
            if (!value.HasValue || double.IsNaN(value.Value)
                || double.IsInfinity(value.Value) || value.Value < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsQueryVolumeKind(DvhVolumeKind kind)
        {
            return kind == DvhVolumeKind.RelativePercent
                || kind == DvhVolumeKind.AbsoluteCm3;
        }
    }

    public sealed class DvhSourceValue
    {
        private DvhSourceValue(
            bool isAvailable,
            double value,
            DvhValueUnit unit,
            string nativeUnit,
            MetricCalculationStatus unavailableStatus,
            string reason)
        {
            IsAvailable = isAvailable;
            Value = value;
            Unit = unit;
            NativeUnit = nativeUnit;
            UnavailableStatus = unavailableStatus;
            Reason = reason;
        }

        public bool IsAvailable { get; private set; }
        public double Value { get; private set; }
        public DvhValueUnit Unit { get; private set; }
        public string NativeUnit { get; private set; }
        public MetricCalculationStatus UnavailableStatus { get; private set; }
        public string Reason { get; private set; }

        public static DvhSourceValue Available(
            double value,
            DvhValueUnit unit,
            string nativeUnit)
        {
            return new DvhSourceValue(
                true, value, unit, nativeUnit, MetricCalculationStatus.Available, null);
        }

        public static DvhSourceValue Unavailable(
            MetricCalculationStatus status,
            string reason)
        {
            if (status == MetricCalculationStatus.Available)
            {
                throw new ArgumentException("Unavailable status cannot be Available.", "status");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Unavailable source reason is required.", "reason");
            }

            return new DvhSourceValue(false, 0.0, DvhValueUnit.Unknown, null, status, reason);
        }
    }

    /// <summary>
    /// Testable boundary around the three documented PlanningItem DVH queries.
    /// </summary>
    public interface IDvhDataSource
    {
        bool HasDose { get; }
        string StructureId { get; }
        DvhSourceValue GetMeanDose(double binWidthGy);
        DvhSourceValue GetVolumeAtDose(double doseGy, DvhVolumeKind volumeKind);
        DvhSourceValue GetDoseAtVolume(double volume, DvhVolumeKind volumeKind);
    }

    public sealed class DvhMetricEvaluator
    {
        public MetricCalculationResult Evaluate(
            IDvhDataSource source,
            DvhMetricRequest request,
            double binWidthGy)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (double.IsNaN(binWidthGy) || double.IsInfinity(binWidthGy) || binWidthGy <= 0.0)
            {
                throw new ArgumentOutOfRangeException("binWidthGy");
            }

            try
            {
                if (!source.HasDose)
                {
                    return MetricCalculationResult.Unavailable(
                        request.Name,
                        source.StructureId,
                        MetricCalculationStatus.MissingData,
                        "The reviewed plan has no available dose.");
                }

                DvhSourceValue value;
                switch (request.Kind)
                {
                    case DvhMetricKind.MeanDose:
                        value = source.GetMeanDose(binWidthGy);
                        break;
                    case DvhMetricKind.VolumeAtDose:
                        value = source.GetVolumeAtDose(
                            request.DoseGy.Value,
                            request.VolumeKind);
                        break;
                    case DvhMetricKind.DoseAtVolume:
                        value = source.GetDoseAtVolume(
                            request.Volume.Value,
                            request.VolumeKind);
                        break;
                    default:
                        return MetricCalculationResult.Unavailable(
                            request.Name,
                            source.StructureId,
                            MetricCalculationStatus.Unsupported,
                            "The configured DVH metric type is unsupported.");
                }

                return Normalize(request, source.StructureId, value);
            }
            catch (Exception exception)
            {
                return MetricCalculationResult.Unavailable(
                    request.Name,
                    source.StructureId,
                    MetricCalculationStatus.CalculationFailed,
                    "DVH query failed: " + exception.Message);
            }
        }

        private static MetricCalculationResult Normalize(
            DvhMetricRequest request,
            string structureId,
            DvhSourceValue sourceValue)
        {
            if (sourceValue == null)
            {
                return MetricCalculationResult.Unavailable(
                    request.Name,
                    structureId,
                    MetricCalculationStatus.MissingData,
                    "The DVH query returned no result.");
            }

            if (!sourceValue.IsAvailable)
            {
                return MetricCalculationResult.Unavailable(
                    request.Name,
                    structureId,
                    sourceValue.UnavailableStatus,
                    sourceValue.Reason);
            }

            if (double.IsNaN(sourceValue.Value)
                || double.IsInfinity(sourceValue.Value)
                || sourceValue.Value < 0.0)
            {
                return MetricCalculationResult.Unavailable(
                    request.Name,
                    structureId,
                    MetricCalculationStatus.CalculationFailed,
                    "The DVH query returned an invalid value.");
            }

            if (request.Kind == DvhMetricKind.VolumeAtDose)
            {
                DvhValueUnit expected = request.VolumeKind == DvhVolumeKind.RelativePercent
                    ? DvhValueUnit.RelativePercent
                    : DvhValueUnit.AbsoluteCm3;
                if (sourceValue.Unit != expected)
                {
                    return UnsupportedUnit(request, structureId, sourceValue);
                }

                return MetricCalculationResult.Available(
                    request.Name,
                    structureId,
                    sourceValue.Value,
                    expected == DvhValueUnit.RelativePercent ? "%" : "cc",
                    sourceValue.NativeUnit);
            }

            double gy;
            if (sourceValue.Unit == DvhValueUnit.Gy)
            {
                gy = sourceValue.Value;
            }
            else if (sourceValue.Unit == DvhValueUnit.Centigray)
            {
                gy = sourceValue.Value / 100.0;
            }
            else
            {
                return UnsupportedUnit(request, structureId, sourceValue);
            }

            return MetricCalculationResult.Available(
                request.Name,
                structureId,
                gy,
                "Gy",
                sourceValue.NativeUnit);
        }

        private static MetricCalculationResult UnsupportedUnit(
            DvhMetricRequest request,
            string structureId,
            DvhSourceValue sourceValue)
        {
            return MetricCalculationResult.Unavailable(
                request.Name,
                structureId,
                MetricCalculationStatus.Unsupported,
                "The DVH query returned unsupported unit '"
                    + (sourceValue.NativeUnit ?? sourceValue.Unit.ToString())
                    + "'.");
        }
    }
}
