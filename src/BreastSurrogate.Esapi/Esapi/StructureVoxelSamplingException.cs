using System;

namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// Describes an expected input failure while preparing or sampling a structure.
    /// </summary>
    public sealed class StructureVoxelSamplingException : Exception
    {
        public StructureVoxelSamplingException(string message)
            : base(message)
        {
        }
    }
}
