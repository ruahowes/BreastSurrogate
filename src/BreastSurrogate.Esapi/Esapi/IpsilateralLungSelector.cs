using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// Selects the explicitly named ipsilateral lung, or conservatively chooses
    /// between the two whole-lung IDs used by the newer template.
    /// </summary>
    public sealed class IpsilateralLungSelector
    {
        public const string PreferredStructureId = "IPS LUNG";
        public const string LeftLungStructureId = "Lung_L";
        public const string RightLungStructureId = "Lung_R";

        private const double DistanceTieToleranceMm = 0.01;

        public IpsilateralLungSelectionResult Select(
            IEnumerable<Structure> structures,
            VVector referenceIsocentre)
        {
            if (structures == null)
            {
                throw new ArgumentNullException("structures");
            }

            if (!IsFinite(referenceIsocentre))
            {
                throw new ArgumentException(
                    "The reference isocentre must contain finite DICOM coordinates.",
                    "referenceIsocentre");
            }

            List<Structure> structureList = structures.Where(
                structure => structure != null).ToList();
            List<Structure> preferredMatches = structureList.Where(
                structure => string.Equals(
                    structure.Id,
                    PreferredStructureId,
                    StringComparison.OrdinalIgnoreCase)).ToList();

            if (preferredMatches.Count > 1)
            {
                throw new IpsilateralLungSelectionException(
                    "More than one structure matches preferred ID '"
                    + PreferredStructureId
                    + "' case-insensitively.",
                    IpsilateralLungSelectionFailureKind.Ambiguous);
            }

            if (preferredMatches.Count == 1)
            {
                IpsilateralLungCandidate preferred = CreateCandidate(
                    preferredMatches[0],
                    referenceIsocentre,
                    true);
                return new IpsilateralLungSelectionResult(
                    preferred.Structure,
                    "Preferred structure ID '" + PreferredStructureId + "'",
                    referenceIsocentre,
                    new List<IpsilateralLungCandidate> { preferred });
            }

            List<Structure> recognizedFallbacks = structureList.Where(
                structure => IsFallbackWholeLungId(structure.Id)).ToList();
            List<IpsilateralLungCandidate> candidates = recognizedFallbacks
                .Where(IsUsable)
                .Select(structure => CreateCandidate(structure, referenceIsocentre, false))
                .OrderBy(candidate => candidate.DistanceToIsocentreMm)
                .ThenBy(candidate => candidate.Structure.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count == 0)
            {
                throw new IpsilateralLungSelectionException(
                    "No usable ipsilateral-lung structure was found. Expected preferred ID '"
                    + PreferredStructureId
                    + "', or a non-empty recognized whole-lung ID such as '"
                    + LeftLungStructureId
                    + "', 'L Lung', '"
                    + RightLungStructureId
                    + "', or 'R Lung'.",
                    IpsilateralLungSelectionFailureKind.MissingData);
            }

            if (candidates.Count > 1
                && Math.Abs(
                    candidates[0].DistanceToIsocentreMm
                    - candidates[1].DistanceToIsocentreMm) <= DistanceTieToleranceMm)
            {
                throw new IpsilateralLungSelectionException(
                    "Fallback lung selection is ambiguous because structures '"
                    + candidates[0].Structure.Id
                    + "' and '"
                    + candidates[1].Structure.Id
                    + "' have centres equally close to isocentre within "
                    + DistanceTieToleranceMm
                    + " mm.",
                    IpsilateralLungSelectionFailureKind.Ambiguous);
            }

            return new IpsilateralLungSelectionResult(
                candidates[0].Structure,
                "Closest recognized whole-lung centre to reference isocentre",
                referenceIsocentre,
                candidates);
        }

        private static IpsilateralLungCandidate CreateCandidate(
            Structure structure,
            VVector referenceIsocentre,
            bool requireUsable)
        {
            if (requireUsable && !IsUsable(structure))
            {
                throw new IpsilateralLungSelectionException(
                    "Preferred structure '"
                    + structure.Id
                    + "' is empty or has no segment.",
                    IpsilateralLungSelectionFailureKind.MissingData);
            }

            VVector centerPoint = structure.CenterPoint;
            if (!IsFinite(centerPoint))
            {
                throw new IpsilateralLungSelectionException(
                    "Structure '" + structure.Id + "' has a non-finite centre point.");
            }

            double distance = VVector.Distance(centerPoint, referenceIsocentre);
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                throw new IpsilateralLungSelectionException(
                    "Structure '" + structure.Id + "' has an invalid distance to isocentre.");
            }

            return new IpsilateralLungCandidate(structure, centerPoint, distance);
        }

        private static bool IsFallbackWholeLungId(string structureId)
        {
            string normalized = StructureIdText.Normalize(structureId);
            return normalized == "LUNGL"
                || normalized == "LLUNG"
                || normalized == "LEFTLUNG"
                || normalized == "LUNGLT"
                || normalized == "LTLUNG"
                || normalized == "LUNGR"
                || normalized == "RLUNG"
                || normalized == "RIGHTLUNG"
                || normalized == "LUNGRT"
                || normalized == "RTLUNG";
        }

        private static bool IsUsable(Structure structure)
        {
            return structure.HasSegment && !structure.IsEmpty;
        }

        private static bool IsFinite(VVector vector)
        {
            return !double.IsNaN(vector.x)
                && !double.IsInfinity(vector.x)
                && !double.IsNaN(vector.y)
                && !double.IsInfinity(vector.y)
                && !double.IsNaN(vector.z)
                && !double.IsInfinity(vector.z);
        }
    }
}
