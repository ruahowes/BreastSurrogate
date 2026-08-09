namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// Aggregate result from full-resolution image-voxel-centre structure sampling.
    /// </summary>
    public sealed class StructureVoxelSamplingResult
    {
        public StructureVoxelSamplingResult(
            string structureId,
            int minimumXIndex,
            int maximumXIndex,
            int minimumYIndex,
            int maximumYIndex,
            int minimumZIndex,
            int maximumZIndex,
            long candidateVoxelCount,
            long insideStructureVoxelCount,
            long structureMembershipQueryCount,
            double voxelVolumeCubicMillimetres,
            double esapiStructureVolumeCubicCentimetres,
            long elapsedMilliseconds)
        {
            StructureId = structureId;
            MinimumXIndex = minimumXIndex;
            MaximumXIndex = maximumXIndex;
            MinimumYIndex = minimumYIndex;
            MaximumYIndex = maximumYIndex;
            MinimumZIndex = minimumZIndex;
            MaximumZIndex = maximumZIndex;
            CandidateVoxelCount = candidateVoxelCount;
            InsideStructureVoxelCount = insideStructureVoxelCount;
            StructureMembershipQueryCount = structureMembershipQueryCount;
            VoxelVolumeCubicMillimetres = voxelVolumeCubicMillimetres;
            EsapiStructureVolumeCubicCentimetres = esapiStructureVolumeCubicCentimetres;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public string StructureId { get; private set; }

        public int MinimumXIndex { get; private set; }

        public int MaximumXIndex { get; private set; }

        public int MinimumYIndex { get; private set; }

        public int MaximumYIndex { get; private set; }

        public int MinimumZIndex { get; private set; }

        public int MaximumZIndex { get; private set; }

        public long CandidateVoxelCount { get; private set; }

        public long InsideStructureVoxelCount { get; private set; }

        public long StructureMembershipQueryCount { get; private set; }

        public double VoxelVolumeCubicMillimetres { get; private set; }

        public double SampledStructureVolumeCubicCentimetres
        {
            get
            {
                return InsideStructureVoxelCount * VoxelVolumeCubicMillimetres / 1000.0;
            }
        }

        public double EsapiStructureVolumeCubicCentimetres { get; private set; }

        public long ElapsedMilliseconds { get; private set; }
    }
}
