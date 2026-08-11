using System;

namespace BreastSurrogate.Esapi.Esapi
{
    public enum IpsilateralLungSelectionFailureKind
    {
        MissingData,
        Ambiguous,
        InvalidData
    }

    public sealed class IpsilateralLungSelectionException : Exception
    {
        public IpsilateralLungSelectionException(string message)
            : this(message, IpsilateralLungSelectionFailureKind.InvalidData)
        {
        }

        public IpsilateralLungSelectionException(
            string message,
            IpsilateralLungSelectionFailureKind failureKind)
            : base(message)
        {
            FailureKind = failureKind;
        }

        public IpsilateralLungSelectionFailureKind FailureKind { get; private set; }
    }
}
