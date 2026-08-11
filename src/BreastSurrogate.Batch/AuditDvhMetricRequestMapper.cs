using System;
using System.Collections.Generic;
using System.Linq;
using BreastSurrogate.Esapi.Esapi;

namespace BreastSurrogate.Batch
{
    public static class AuditDvhMetricRequestMapper
    {
        public static IList<ReviewedPlanMetricRequest> Create(
            IEnumerable<AuditMetricConfiguration> configurations)
        {
            if (configurations == null)
            {
                throw new ArgumentNullException("configurations");
            }

            return configurations.Select(Create).ToList().AsReadOnly();
        }

        public static ReviewedPlanMetricRequest Create(
            AuditMetricConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            DvhMetricKind kind;
            switch (configuration.Type)
            {
                case AuditMetricType.MeanDose:
                    kind = DvhMetricKind.MeanDose;
                    break;
                case AuditMetricType.VolumeAtDose:
                    kind = DvhMetricKind.VolumeAtDose;
                    break;
                case AuditMetricType.DoseAtVolume:
                    kind = DvhMetricKind.DoseAtVolume;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("configuration");
            }

            DvhVolumeKind volumeKind;
            switch (configuration.VolumePresentation)
            {
                case AuditVolumePresentation.None:
                    volumeKind = DvhVolumeKind.None;
                    break;
                case AuditVolumePresentation.RelativePercent:
                    volumeKind = DvhVolumeKind.RelativePercent;
                    break;
                case AuditVolumePresentation.AbsoluteCc:
                    volumeKind = DvhVolumeKind.AbsoluteCm3;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("configuration");
            }

            return new ReviewedPlanMetricRequest(
                configuration.Structure,
                new DvhMetricRequest(
                    configuration.Name,
                    kind,
                    configuration.DoseGy,
                    configuration.Volume,
                    volumeKind));
        }
    }
}
