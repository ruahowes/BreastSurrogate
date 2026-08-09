using System;
using BreastSurrogate.Core.Apertures;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Calculation
{
    /// <summary>
    /// Classifies patient-space structure points against two jaw-only apertures.
    /// </summary>
    public sealed class JawInFieldPointClassifier
    {
        private readonly StaticBeamAperture _field1;
        private readonly StaticBeamAperture _field2;
        private readonly InFieldSampleAccumulator _accumulator;

        public JawInFieldPointClassifier(
            StaticBeamAperture field1,
            StaticBeamAperture field2)
        {
            if (field1 == null)
            {
                throw new ArgumentNullException("field1");
            }

            if (field2 == null)
            {
                throw new ArgumentNullException("field2");
            }

            _field1 = field1;
            _field2 = field2;
            _accumulator = new InFieldSampleAccumulator();
        }

        public void Add(VVector patientPoint)
        {
            _accumulator.Add(
                _field1.Contains(patientPoint),
                _field2.Contains(patientPoint));
        }

        public InFieldCalculationResult CreateResult(
            double voxelVolumeCubicMillimetres,
            double esapiStructureVolumeCubicCentimetres)
        {
            return _accumulator.CreateResult(
                voxelVolumeCubicMillimetres,
                esapiStructureVolumeCubicCentimetres);
        }
    }
}
