using System;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// Thin read-only adapter over documented PlanningItem DVH query methods.
    /// </summary>
    public sealed class EsapiDvhDataSource : IDvhDataSource
    {
        private readonly PlanningItem _planningItem;
        private readonly Structure _structure;

        public EsapiDvhDataSource(PlanningItem planningItem, Structure structure)
        {
            if (planningItem == null)
            {
                throw new ArgumentNullException("planningItem");
            }

            if (structure == null)
            {
                throw new ArgumentNullException("structure");
            }

            _planningItem = planningItem;
            _structure = structure;
        }

        public bool HasDose
        {
            get
            {
                PlanSetup plan = _planningItem as PlanSetup;
                return _planningItem.Dose != null && (plan == null || plan.IsDoseValid);
            }
        }
        public string StructureId { get { return _structure.Id; } }

        public DvhSourceValue GetMeanDose(double binWidthGy)
        {
            DVHData dvh = _planningItem.GetDVHCumulativeData(
                _structure,
                DoseValuePresentation.Absolute,
                VolumePresentation.AbsoluteCm3,
                binWidthGy);
            if (dvh == null)
            {
                return DvhSourceValue.Unavailable(
                    MetricCalculationStatus.MissingData,
                    "ESAPI could not calculate cumulative DVH data for structure '"
                        + _structure.Id + "'.");
            }

            return FromDoseValue(dvh.MeanDose);
        }

        public DvhSourceValue GetVolumeAtDose(
            double doseGy,
            DvhVolumeKind volumeKind)
        {
            VolumePresentation presentation = ToVolumePresentation(volumeKind);
            double value = _planningItem.GetVolumeAtDose(
                _structure,
                new DoseValue(doseGy, DoseValue.DoseUnit.Gy),
                presentation);
            return DvhSourceValue.Available(
                value,
                volumeKind == DvhVolumeKind.RelativePercent
                    ? DvhValueUnit.RelativePercent
                    : DvhValueUnit.AbsoluteCm3,
                DoseValue.DoseUnit.Gy.ToString());
        }

        public DvhSourceValue GetDoseAtVolume(
            double volume,
            DvhVolumeKind volumeKind)
        {
            DoseValue value = _planningItem.GetDoseAtVolume(
                _structure,
                volume,
                ToVolumePresentation(volumeKind),
                DoseValuePresentation.Absolute);
            return FromDoseValue(value);
        }

        private static VolumePresentation ToVolumePresentation(DvhVolumeKind kind)
        {
            if (kind == DvhVolumeKind.RelativePercent)
            {
                return VolumePresentation.Relative;
            }

            if (kind == DvhVolumeKind.AbsoluteCm3)
            {
                return VolumePresentation.AbsoluteCm3;
            }

            throw new ArgumentOutOfRangeException("kind");
        }

        private static DvhSourceValue FromDoseValue(DoseValue value)
        {
            DvhValueUnit unit;
            switch (value.Unit)
            {
                case DoseValue.DoseUnit.Gy:
                    unit = DvhValueUnit.Gy;
                    break;
                case DoseValue.DoseUnit.cGy:
                    unit = DvhValueUnit.Centigray;
                    break;
                default:
                    unit = DvhValueUnit.Unknown;
                    break;
            }

            return DvhSourceValue.Available(value.Dose, unit, value.UnitAsString);
        }
    }
}
