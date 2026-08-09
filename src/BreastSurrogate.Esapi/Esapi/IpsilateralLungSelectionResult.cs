using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Esapi
{
    public sealed class IpsilateralLungSelectionResult
    {
        public IpsilateralLungSelectionResult(
            Structure selectedStructure,
            string selectionMethod,
            VVector referenceIsocentre,
            IList<IpsilateralLungCandidate> candidates)
        {
            SelectedStructure = selectedStructure;
            SelectionMethod = selectionMethod;
            ReferenceIsocentre = referenceIsocentre;
            Candidates = new List<IpsilateralLungCandidate>(candidates).AsReadOnly();
        }

        public Structure SelectedStructure { get; private set; }

        public string SelectionMethod { get; private set; }

        public VVector ReferenceIsocentre { get; private set; }

        public IList<IpsilateralLungCandidate> Candidates { get; private set; }
    }

    public sealed class IpsilateralLungCandidate
    {
        public IpsilateralLungCandidate(
            Structure structure,
            VVector centerPoint,
            double distanceToIsocentreMm)
        {
            Structure = structure;
            CenterPoint = centerPoint;
            DistanceToIsocentreMm = distanceToIsocentreMm;
        }

        public Structure Structure { get; private set; }

        public VVector CenterPoint { get; private set; }

        public double DistanceToIsocentreMm { get; private set; }
    }
}
