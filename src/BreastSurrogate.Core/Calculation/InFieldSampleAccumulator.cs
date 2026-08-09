using System;

namespace BreastSurrogate.Core.Calculation
{
    /// <summary>
    /// Deterministically accumulates membership in two fields and their union/intersection.
    /// </summary>
    public sealed class InFieldSampleAccumulator
    {
        private long _totalStructurePointCount;
        private long _field1PointCount;
        private long _field2PointCount;
        private long _eitherFieldPointCount;
        private long _bothFieldsPointCount;

        public void Add(bool insideField1, bool insideField2)
        {
            checked
            {
                _totalStructurePointCount++;

                if (insideField1)
                {
                    _field1PointCount++;
                }

                if (insideField2)
                {
                    _field2PointCount++;
                }

                if (insideField1 || insideField2)
                {
                    _eitherFieldPointCount++;
                }

                if (insideField1 && insideField2)
                {
                    _bothFieldsPointCount++;
                }
            }
        }

        public InFieldCalculationResult CreateResult(
            double voxelVolumeCubicMillimetres,
            double esapiStructureVolumeCubicCentimetres)
        {
            if (!IsFinitePositive(voxelVolumeCubicMillimetres))
            {
                throw new ArgumentOutOfRangeException(
                    "voxelVolumeCubicMillimetres",
                    "Voxel volume must be finite and positive.");
            }

            if (!IsFinitePositive(esapiStructureVolumeCubicCentimetres))
            {
                throw new ArgumentOutOfRangeException(
                    "esapiStructureVolumeCubicCentimetres",
                    "ESAPI structure volume must be finite and positive.");
            }

            return new InFieldCalculationResult(
                _totalStructurePointCount,
                _field1PointCount,
                _field2PointCount,
                _eitherFieldPointCount,
                _bothFieldsPointCount,
                voxelVolumeCubicMillimetres,
                esapiStructureVolumeCubicCentimetres);
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        }
    }
}
