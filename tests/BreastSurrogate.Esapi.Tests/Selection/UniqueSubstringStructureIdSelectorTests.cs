using System.Collections.Generic;
using BreastSurrogate.Esapi.Esapi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Esapi.Tests.Selection
{
    [TestClass]
    public class UniqueSubstringStructureIdSelectorTests
    {
        [TestMethod]
        public void SelectsUniqueUsableCaseInsensitiveSubstringMatch()
        {
            StructureIdSelectionResult result = UniqueSubstringStructureIdSelector.Select(
                Candidates(
                    new StructureIdCandidate("IPS LUNG", true),
                    new StructureIdCandidate("ilf 12.7%", true),
                    new StructureIdCandidate("Heart", true)),
                "ILF",
                "legacy ILF");

            Assert.IsTrue(result.IsSelected);
            Assert.AreEqual("ilf 12.7%", result.SelectedStructureId);
            Assert.AreEqual(1, result.Candidates.Count);
        }

        [TestMethod]
        public void IgnoresEmptyMatchWhenExactlyOneUsableMatchExists()
        {
            StructureIdSelectionResult result = UniqueSubstringStructureIdSelector.Select(
                Candidates(
                    new StructureIdCandidate("ILF old", false),
                    new StructureIdCandidate("ILF 12.7%", true)),
                "ILF",
                "legacy ILF");

            Assert.IsTrue(result.IsSelected);
            Assert.AreEqual("ILF 12.7%", result.SelectedStructureId);
            Assert.AreEqual(2, result.Candidates.Count);
        }

        [TestMethod]
        public void MultipleUsableMatchesAreUnavailable()
        {
            StructureIdSelectionResult result = UniqueSubstringStructureIdSelector.Select(
                Candidates(
                    new StructureIdCandidate("ILF", true),
                    new StructureIdCandidate("ILF 12.7%", true)),
                "ILF",
                "legacy ILF");

            Assert.IsFalse(result.IsSelected);
            StringAssert.Contains(result.FailureReason, "ambiguous");
        }

        [TestMethod]
        public void MissingAndOnlyEmptyMatchesAreUnavailable()
        {
            StructureIdSelectionResult missing = UniqueSubstringStructureIdSelector.Select(
                Candidates(new StructureIdCandidate("Heart", true)),
                "HIF",
                "legacy HIF");
            StructureIdSelectionResult empty = UniqueSubstringStructureIdSelector.Select(
                Candidates(new StructureIdCandidate("HIF", false)),
                "HIF",
                "legacy HIF");

            Assert.IsFalse(missing.IsSelected);
            StringAssert.Contains(missing.FailureReason, "No legacy HIF");
            Assert.IsFalse(empty.IsSelected);
            StringAssert.Contains(empty.FailureReason, "all were empty");
        }

        private static IEnumerable<StructureIdCandidate> Candidates(
            params StructureIdCandidate[] candidates)
        {
            return candidates;
        }
    }
}
