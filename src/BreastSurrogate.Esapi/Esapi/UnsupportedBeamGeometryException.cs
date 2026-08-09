using System;

namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// Indicates that an ESAPI beam is outside the explicitly supported geometry.
    /// </summary>
    public sealed class UnsupportedBeamGeometryException : NotSupportedException
    {
        public UnsupportedBeamGeometryException(string message)
            : base(message)
        {
        }

        public UnsupportedBeamGeometryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
