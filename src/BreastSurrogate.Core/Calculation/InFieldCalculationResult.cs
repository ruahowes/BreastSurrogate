namespace BreastSurrogate.Core.Calculation
{
    /// <summary>
    /// Immutable jaw-field overlap counts, volumes and percentages for one structure.
    /// Percentages use the supplied ESAPI structure volume as their denominator.
    /// </summary>
    public sealed class InFieldCalculationResult
    {
        internal InFieldCalculationResult(
            long totalStructurePointCount,
            long field1PointCount,
            long field2PointCount,
            long eitherFieldPointCount,
            long bothFieldsPointCount,
            double voxelVolumeCubicMillimetres,
            double esapiStructureVolumeCubicCentimetres)
        {
            TotalStructurePointCount = totalStructurePointCount;
            Field1PointCount = field1PointCount;
            Field2PointCount = field2PointCount;
            EitherFieldPointCount = eitherFieldPointCount;
            BothFieldsPointCount = bothFieldsPointCount;
            VoxelVolumeCubicMillimetres = voxelVolumeCubicMillimetres;
            EsapiStructureVolumeCubicCentimetres = esapiStructureVolumeCubicCentimetres;
        }

        public long TotalStructurePointCount { get; private set; }

        public long Field1PointCount { get; private set; }

        public long Field2PointCount { get; private set; }

        public long EitherFieldPointCount { get; private set; }

        public long BothFieldsPointCount { get; private set; }

        public double VoxelVolumeCubicMillimetres { get; private set; }

        public double EsapiStructureVolumeCubicCentimetres { get; private set; }

        public double SampledStructureVolumeCubicCentimetres
        {
            get { return PointCountToVolume(TotalStructurePointCount); }
        }

        public double Field1VolumeCubicCentimetres
        {
            get { return PointCountToVolume(Field1PointCount); }
        }

        public double Field2VolumeCubicCentimetres
        {
            get { return PointCountToVolume(Field2PointCount); }
        }

        public double EitherFieldVolumeCubicCentimetres
        {
            get { return PointCountToVolume(EitherFieldPointCount); }
        }

        public double BothFieldsVolumeCubicCentimetres
        {
            get { return PointCountToVolume(BothFieldsPointCount); }
        }

        public double Field1PercentageOfEsapiVolume
        {
            get { return VolumeToPercentage(Field1VolumeCubicCentimetres); }
        }

        public double Field2PercentageOfEsapiVolume
        {
            get { return VolumeToPercentage(Field2VolumeCubicCentimetres); }
        }

        public double EitherFieldPercentageOfEsapiVolume
        {
            get { return VolumeToPercentage(EitherFieldVolumeCubicCentimetres); }
        }

        public double BothFieldsPercentageOfEsapiVolume
        {
            get { return VolumeToPercentage(BothFieldsVolumeCubicCentimetres); }
        }

        private double PointCountToVolume(long pointCount)
        {
            return pointCount * VoxelVolumeCubicMillimetres / 1000.0;
        }

        private double VolumeToPercentage(double volumeCubicCentimetres)
        {
            return 100.0 * volumeCubicCentimetres / EsapiStructureVolumeCubicCentimetres;
        }
    }
}
