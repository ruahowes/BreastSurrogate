using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// ESAPI-independent structure-ID text operations used by deterministic selectors.
    /// </summary>
    public static class StructureIdText
    {
        public static string Normalize(string structureId)
        {
            if (string.IsNullOrWhiteSpace(structureId))
            {
                return string.Empty;
            }

            var normalized = new StringBuilder(structureId.Length);
            foreach (char character in structureId)
            {
                if (char.IsLetterOrDigit(character))
                {
                    normalized.Append(char.ToUpperInvariant(character));
                }
            }

            return normalized.ToString();
        }

        public static bool Contains(string structureId, string token)
        {
            return !string.IsNullOrEmpty(structureId)
                && !string.IsNullOrEmpty(token)
                && structureId.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static int EditDistance(string first, string second)
        {
            if (first == null)
            {
                throw new ArgumentNullException("first");
            }

            if (second == null)
            {
                throw new ArgumentNullException("second");
            }

            var previous = new int[second.Length + 1];
            var current = new int[second.Length + 1];
            for (int column = 0; column <= second.Length; column++)
            {
                previous[column] = column;
            }

            for (int row = 1; row <= first.Length; row++)
            {
                current[0] = row;
                for (int column = 1; column <= second.Length; column++)
                {
                    int substitutionCost = first[row - 1] == second[column - 1]
                        ? 0
                        : 1;
                    current[column] = Math.Min(
                        Math.Min(
                            current[column - 1] + 1,
                            previous[column] + 1),
                        previous[column - 1] + substitutionCost);
                }

                int[] swap = previous;
                previous = current;
                current = swap;
            }

            return previous[second.Length];
        }
    }

    public sealed class StructureIdCandidate
    {
        public StructureIdCandidate(string structureId, bool isUsable)
        {
            StructureId = structureId;
            IsUsable = isUsable;
        }

        public string StructureId { get; private set; }

        public bool IsUsable { get; private set; }
    }

    public sealed class StructureIdCandidateDiagnostics
    {
        public StructureIdCandidateDiagnostics(
            string structureId,
            string normalizedStructureId,
            bool isUsable,
            int? editDistance)
        {
            StructureId = structureId;
            NormalizedStructureId = normalizedStructureId;
            IsUsable = isUsable;
            EditDistance = editDistance;
        }

        public string StructureId { get; private set; }

        public string NormalizedStructureId { get; private set; }

        public bool IsUsable { get; private set; }

        public int? EditDistance { get; private set; }
    }

    public sealed class StructureIdSelectionResult
    {
        private readonly ReadOnlyCollection<StructureIdCandidateDiagnostics> _candidates;

        private StructureIdSelectionResult(
            string selectedStructureId,
            string selectionMethod,
            string failureReason,
            IList<StructureIdCandidateDiagnostics> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException("candidates");
            }

            SelectedStructureId = selectedStructureId;
            SelectionMethod = selectionMethod;
            FailureReason = failureReason;
            _candidates = new ReadOnlyCollection<StructureIdCandidateDiagnostics>(
                new List<StructureIdCandidateDiagnostics>(candidates));
        }

        public bool IsSelected
        {
            get { return !string.IsNullOrWhiteSpace(SelectedStructureId); }
        }

        public string SelectedStructureId { get; private set; }

        public string SelectionMethod { get; private set; }

        public string FailureReason { get; private set; }

        public IList<StructureIdCandidateDiagnostics> Candidates
        {
            get { return _candidates; }
        }

        public static StructureIdSelectionResult Selected(
            string selectedStructureId,
            string selectionMethod,
            IList<StructureIdCandidateDiagnostics> candidates)
        {
            if (string.IsNullOrWhiteSpace(selectedStructureId))
            {
                throw new ArgumentException(
                    "A selected result must identify its structure.",
                    "selectedStructureId");
            }

            if (string.IsNullOrWhiteSpace(selectionMethod))
            {
                throw new ArgumentException(
                    "A selected result must describe its selection method.",
                    "selectionMethod");
            }

            return new StructureIdSelectionResult(
                selectedStructureId,
                selectionMethod,
                null,
                candidates);
        }

        public static StructureIdSelectionResult Unavailable(
            string failureReason,
            IList<StructureIdCandidateDiagnostics> candidates)
        {
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                throw new ArgumentException(
                    "An unavailable result must have a failure reason.",
                    "failureReason");
            }

            return new StructureIdSelectionResult(
                null,
                null,
                failureReason,
                candidates);
        }
    }

    /// <summary>
    /// Pure deterministic Heart ID selector. Input usability is supplied by the ESAPI adapter.
    /// </summary>
    public static class HeartStructureIdSelector
    {
        public const string PreferredStructureId = "Heart";

        public static StructureIdSelectionResult Select(
            IEnumerable<StructureIdCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException("candidates");
            }

            List<StructureIdCandidateDiagnostics> matching = candidates
                .Where(candidate => candidate != null)
                .Where(candidate => StructureIdText.Contains(
                    candidate.StructureId,
                    PreferredStructureId))
                .Select(candidate => new StructureIdCandidateDiagnostics(
                    candidate.StructureId,
                    StructureIdText.Normalize(candidate.StructureId),
                    candidate.IsUsable,
                    StructureIdText.EditDistance(
                        StructureIdText.Normalize(candidate.StructureId),
                        StructureIdText.Normalize(PreferredStructureId))))
                .OrderBy(candidate => candidate.EditDistance.Value)
                .ThenBy(candidate => candidate.StructureId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.StructureId, StringComparer.Ordinal)
                .ToList();

            if (matching.Count == 0)
            {
                return StructureIdSelectionResult.Unavailable(
                    "No structure ID containing 'Heart' was found.",
                    matching);
            }

            List<StructureIdCandidateDiagnostics> usable = matching
                .Where(candidate => candidate.IsUsable)
                .ToList();
            if (usable.Count == 0)
            {
                return StructureIdSelectionResult.Unavailable(
                    "Structure IDs containing 'Heart' were found, but all were empty or had no segment.",
                    matching);
            }

            List<StructureIdCandidateDiagnostics> exact = usable.Where(
                candidate => string.Equals(
                    candidate.StructureId,
                    PreferredStructureId,
                    StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1)
            {
                return StructureIdSelectionResult.Selected(
                    exact[0].StructureId,
                    "Unique non-empty exact ID 'Heart'",
                    matching);
            }

            if (exact.Count > 1)
            {
                return StructureIdSelectionResult.Unavailable(
                    "Heart selection is ambiguous because more than one non-empty structure has exact ID 'Heart' case-insensitively.",
                    matching);
            }

            int minimumDistance = usable.Min(candidate => candidate.EditDistance.Value);
            List<StructureIdCandidateDiagnostics> closest = usable.Where(
                candidate => candidate.EditDistance.Value == minimumDistance).ToList();
            if (closest.Count != 1)
            {
                return StructureIdSelectionResult.Unavailable(
                    "Heart selection is ambiguous because "
                    + closest.Count
                    + " non-empty structures share the closest normalized edit distance of "
                    + minimumDistance
                    + " from 'HEART'.",
                    matching);
            }

            return StructureIdSelectionResult.Selected(
                closest[0].StructureId,
                "Unique closest normalized ID to 'HEART' (edit distance "
                    + minimumDistance
                    + ")",
                matching);
        }
    }

    /// <summary>
    /// Pure deterministic unique-substring selector used for legacy ILF/HIF structures.
    /// </summary>
    public static class UniqueSubstringStructureIdSelector
    {
        public static StructureIdSelectionResult Select(
            IEnumerable<StructureIdCandidate> candidates,
            string requiredToken,
            string structureRole)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException("candidates");
            }

            if (string.IsNullOrWhiteSpace(requiredToken))
            {
                throw new ArgumentException("Required token cannot be null or empty.", "requiredToken");
            }

            if (string.IsNullOrWhiteSpace(structureRole))
            {
                throw new ArgumentException("Structure role cannot be null or empty.", "structureRole");
            }

            List<StructureIdCandidateDiagnostics> matching = candidates
                .Where(candidate => candidate != null)
                .Where(candidate => StructureIdText.Contains(
                    candidate.StructureId,
                    requiredToken))
                .Select(candidate => new StructureIdCandidateDiagnostics(
                    candidate.StructureId,
                    StructureIdText.Normalize(candidate.StructureId),
                    candidate.IsUsable,
                    null))
                .OrderBy(candidate => candidate.StructureId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.StructureId, StringComparer.Ordinal)
                .ToList();
            List<StructureIdCandidateDiagnostics> usable = matching
                .Where(candidate => candidate.IsUsable)
                .ToList();

            if (usable.Count == 1)
            {
                return StructureIdSelectionResult.Selected(
                    usable[0].StructureId,
                    "Unique non-empty ID containing '" + requiredToken + "'",
                    matching);
            }

            if (matching.Count == 0)
            {
                return StructureIdSelectionResult.Unavailable(
                    "No " + structureRole + " structure ID containing '"
                    + requiredToken + "' was found.",
                    matching);
            }

            if (usable.Count == 0)
            {
                return StructureIdSelectionResult.Unavailable(
                    structureRole + " structure IDs containing '"
                    + requiredToken
                    + "' were found, but all were empty or had no segment.",
                    matching);
            }

            return StructureIdSelectionResult.Unavailable(
                structureRole + " selection is ambiguous because "
                + usable.Count
                + " non-empty structure IDs contain '"
                + requiredToken
                + "'.",
                matching);
        }
    }
}
