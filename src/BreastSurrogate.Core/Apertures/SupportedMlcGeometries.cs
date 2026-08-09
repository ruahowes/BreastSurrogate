namespace BreastSurrogate.Core.Apertures
{
    /// <summary>
    /// Explicitly configured MLC geometries supported by Core.
    /// </summary>
    public static class SupportedMlcGeometries
    {
        private static readonly MlcGeometryDefinition Millennium120Definition =
            CreateMillennium120Definition();

        /// <summary>
        /// Varian Millennium 120: 60 pairs spanning -200 to +200 mm at
        /// isocentre, with 10 outer 10 mm pairs on each side and 40 central
        /// 5 mm pairs.
        /// </summary>
        public static MlcGeometryDefinition Millennium120
        {
            get { return Millennium120Definition; }
        }

        private static MlcGeometryDefinition CreateMillennium120Definition()
        {
            const int leafPairCount = 60;
            var boundaries = new double[leafPairCount + 1];
            boundaries[0] = -200.0;

            for (int leafPairIndex = 0; leafPairIndex < leafPairCount; leafPairIndex++)
            {
                double widthMm = leafPairIndex < 10 || leafPairIndex >= 50
                    ? 10.0
                    : 5.0;
                boundaries[leafPairIndex + 1] = boundaries[leafPairIndex] + widthMm;
            }

            return new MlcGeometryDefinition("Millennium 120", boundaries);
        }
    }
}
