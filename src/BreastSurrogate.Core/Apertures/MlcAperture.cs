using System;

namespace BreastSurrogate.Core.Apertures
{
    /// <summary>
    /// Static MLCX opening in BLD coordinates. Leaf positions use the ESAPI
    /// [bank, leaf] convention: bank 0 is the negative-X bank and bank 1 is
    /// the positive-X bank.
    /// </summary>
    public sealed class MlcAperture
    {
        private const double BoundaryToleranceMm = 1e-9;
        private readonly double[,] _leafPositionsMm;

        public MlcAperture(
            MlcGeometryDefinition geometryDefinition,
            double[,] leafPositionsMm)
        {
            if (geometryDefinition == null)
            {
                throw new ArgumentNullException("geometryDefinition");
            }

            if (leafPositionsMm == null)
            {
                throw new ArgumentNullException("leafPositionsMm");
            }

            if (leafPositionsMm.GetLength(0) != 2
                || leafPositionsMm.GetLength(1) != geometryDefinition.LeafPairCount)
            {
                throw new ArgumentException(
                    "Leaf positions must have dimensions [2, geometry leaf-pair count].",
                    "leafPositionsMm");
            }

            _leafPositionsMm = (double[,])leafPositionsMm.Clone();
            for (int bankIndex = 0; bankIndex < 2; bankIndex++)
            {
                for (int leafPairIndex = 0;
                    leafPairIndex < geometryDefinition.LeafPairCount;
                    leafPairIndex++)
                {
                    if (!IsFinite(_leafPositionsMm[bankIndex, leafPairIndex]))
                    {
                        throw new ArgumentException(
                            "MLC leaf positions must be finite.",
                            "leafPositionsMm");
                    }
                }
            }

            GeometryDefinition = geometryDefinition;
        }

        public MlcGeometryDefinition GeometryDefinition { get; private set; }

        public double GetNegativeXBankPositionMm(int leafPairIndex)
        {
            ValidateLeafPairIndex(leafPairIndex);
            return _leafPositionsMm[0, leafPairIndex];
        }

        public double GetPositiveXBankPositionMm(int leafPairIndex)
        {
            ValidateLeafPairIndex(leafPairIndex);
            return _leafPositionsMm[1, leafPairIndex];
        }

        public bool Contains(double xBld, double yBld)
        {
            if (!IsFinite(xBld))
            {
                throw new ArgumentOutOfRangeException("xBld", "BLD coordinate must be finite.");
            }

            int leafPairIndex;
            if (!GeometryDefinition.TryGetLeafPairIndex(yBld, out leafPairIndex))
            {
                return false;
            }

            double negativeXEdge = _leafPositionsMm[0, leafPairIndex];
            double positiveXEdge = _leafPositionsMm[1, leafPairIndex];

            // Equal or crossed leaf tips have no finite-width opening.
            if (negativeXEdge >= positiveXEdge)
            {
                return false;
            }

            return xBld >= negativeXEdge - BoundaryToleranceMm
                && xBld <= positiveXEdge + BoundaryToleranceMm;
        }

        private void ValidateLeafPairIndex(int leafPairIndex)
        {
            if (leafPairIndex < 0 || leafPairIndex >= GeometryDefinition.LeafPairCount)
            {
                throw new ArgumentOutOfRangeException("leafPairIndex");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
