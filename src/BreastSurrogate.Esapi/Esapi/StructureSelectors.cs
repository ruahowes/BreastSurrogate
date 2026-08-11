using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace BreastSurrogate.Esapi.Esapi
{
    public sealed class EsapiStructureSelectionResult
    {
        public EsapiStructureSelectionResult(
            Structure selectedStructure,
            StructureIdSelectionResult diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException("diagnostics");
            }

            if (diagnostics.IsSelected != (selectedStructure != null))
            {
                throw new ArgumentException(
                    "Selected structure and ID-selection status must agree.",
                    "selectedStructure");
            }

            SelectedStructure = selectedStructure;
            Diagnostics = diagnostics;
        }

        public Structure SelectedStructure { get; private set; }

        public StructureIdSelectionResult Diagnostics { get; private set; }

        public bool IsSelected
        {
            get { return SelectedStructure != null; }
        }
    }

    /// <summary>
    /// Thin ESAPI adapter for the pure deterministic Heart ID selector.
    /// </summary>
    public sealed class HeartStructureSelector
    {
        public EsapiStructureSelectionResult Select(IEnumerable<Structure> structures)
        {
            if (structures == null)
            {
                throw new ArgumentNullException("structures");
            }

            List<Structure> structureList = structures
                .Where(structure => structure != null)
                .ToList();
            StructureIdSelectionResult diagnostics = HeartStructureIdSelector.Select(
                structureList.Select(structure => new StructureIdCandidate(
                    structure.Id,
                    IsUsable(structure))));
            Structure selected = diagnostics.IsSelected
                ? structureList.Single(structure => IsUsable(structure)
                    && string.Equals(
                        structure.Id,
                        diagnostics.SelectedStructureId,
                        StringComparison.Ordinal))
                : null;

            return new EsapiStructureSelectionResult(selected, diagnostics);
        }

        private static bool IsUsable(Structure structure)
        {
            return structure.HasSegment && !structure.IsEmpty;
        }
    }

    public enum LegacySurrogateStructureKind
    {
        Ilf,
        Hif
    }

    /// <summary>
    /// Thin ESAPI adapter for strict legacy ILF/HIF substring selection.
    /// </summary>
    public sealed class LegacySurrogateStructureSelector
    {
        public EsapiStructureSelectionResult Select(
            IEnumerable<Structure> structures,
            LegacySurrogateStructureKind kind)
        {
            if (structures == null)
            {
                throw new ArgumentNullException("structures");
            }

            string token;
            string role;
            switch (kind)
            {
                case LegacySurrogateStructureKind.Ilf:
                    token = "ILF";
                    role = "legacy ILF";
                    break;
                case LegacySurrogateStructureKind.Hif:
                    token = "HIF";
                    role = "legacy HIF";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("kind");
            }

            List<Structure> structureList = structures
                .Where(structure => structure != null)
                .ToList();
            StructureIdSelectionResult diagnostics = UniqueSubstringStructureIdSelector.Select(
                structureList.Select(structure => new StructureIdCandidate(
                    structure.Id,
                    IsUsable(structure))),
                token,
                role);
            Structure selected = diagnostics.IsSelected
                ? structureList.Single(structure => IsUsable(structure)
                    && string.Equals(
                        structure.Id,
                        diagnostics.SelectedStructureId,
                        StringComparison.Ordinal))
                : null;

            return new EsapiStructureSelectionResult(selected, diagnostics);
        }

        private static bool IsUsable(Structure structure)
        {
            return structure.HasSegment && !structure.IsEmpty;
        }
    }
}
