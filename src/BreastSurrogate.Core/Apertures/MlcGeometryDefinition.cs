using System;

namespace BreastSurrogate.Core.Apertures
{
    /// <summary>
    /// Physical leaf-pair boundaries in the BLD Y direction, ordered from
    /// negative Y to positive Y and expressed at isocentre in millimetres.
    /// </summary>
    public sealed class MlcGeometryDefinition
    {
        private readonly double[] _leafBoundariesMm;

        public MlcGeometryDefinition(string modelIdentifier, double[] leafBoundariesMm)
        {
            if (string.IsNullOrWhiteSpace(modelIdentifier))
            {
                throw new ArgumentException("An MLC model identifier is required.", "modelIdentifier");
            }

            if (leafBoundariesMm == null)
            {
                throw new ArgumentNullException("leafBoundariesMm");
            }

            if (leafBoundariesMm.Length < 2)
            {
                throw new ArgumentException(
                    "At least two leaf boundaries are required.",
                    "leafBoundariesMm");
            }

            _leafBoundariesMm = (double[])leafBoundariesMm.Clone();
            for (int index = 0; index < _leafBoundariesMm.Length; index++)
            {
                if (!IsFinite(_leafBoundariesMm[index]))
                {
                    throw new ArgumentException(
                        "MLC leaf boundaries must be finite.",
                        "leafBoundariesMm");
                }

                if (index > 0 && _leafBoundariesMm[index] <= _leafBoundariesMm[index - 1])
                {
                    throw new ArgumentException(
                        "MLC leaf boundaries must be strictly increasing.",
                        "leafBoundariesMm");
                }
            }

            ModelIdentifier = modelIdentifier;
        }

        public string ModelIdentifier { get; private set; }

        public int LeafPairCount
        {
            get { return _leafBoundariesMm.Length - 1; }
        }

        public double MinimumYBldMm
        {
            get { return _leafBoundariesMm[0]; }
        }

        public double MaximumYBldMm
        {
            get { return _leafBoundariesMm[_leafBoundariesMm.Length - 1]; }
        }

        public double GetLeafLowerBoundaryMm(int leafPairIndex)
        {
            ValidateLeafPairIndex(leafPairIndex);
            return _leafBoundariesMm[leafPairIndex];
        }

        public double GetLeafUpperBoundaryMm(int leafPairIndex)
        {
            ValidateLeafPairIndex(leafPairIndex);
            return _leafBoundariesMm[leafPairIndex + 1];
        }

        /// <summary>
        /// Selects a leaf pair using [lower, upper) intervals. The final physical
        /// upper boundary is included in the final pair. An internal boundary is
        /// therefore assigned to the pair on its positive-Y side.
        /// </summary>
        public bool TryGetLeafPairIndex(double yBld, out int leafPairIndex)
        {
            if (!IsFinite(yBld))
            {
                throw new ArgumentOutOfRangeException("yBld", "BLD coordinate must be finite.");
            }

            if (yBld < MinimumYBldMm || yBld > MaximumYBldMm)
            {
                leafPairIndex = -1;
                return false;
            }

            if (yBld == MaximumYBldMm)
            {
                leafPairIndex = LeafPairCount - 1;
                return true;
            }

            int boundaryIndex = Array.BinarySearch(_leafBoundariesMm, yBld);
            if (boundaryIndex >= 0)
            {
                leafPairIndex = boundaryIndex;
                return true;
            }

            int insertionIndex = ~boundaryIndex;
            leafPairIndex = insertionIndex - 1;
            return true;
        }

        private void ValidateLeafPairIndex(int leafPairIndex)
        {
            if (leafPairIndex < 0 || leafPairIndex >= LeafPairCount)
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
