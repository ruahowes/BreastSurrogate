using System.Collections.Generic;
using System.Linq;
using BreastSurrogate.Esapi.Esapi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Esapi.Tests.Selection
{
    [TestClass]
    public class HeartStructureIdSelectorTests
    {
        [TestMethod]
        public void UniqueUsableExactIdWins()
        {
            StructureIdSelectionResult result = HeartStructureIdSelector.Select(
                Candidates(
                    Candidate("Heart_PRV", true),
                    Candidate("HEART", true),
                    Candidate("Avoid Heart", true)));

            Assert.IsTrue(result.IsSelected);
            Assert.AreEqual("HEART", result.SelectedStructureId);
            StringAssert.Contains(result.SelectionMethod, "exact ID");
            Assert.AreEqual(3, result.Candidates.Count);
        }

        [TestMethod]
        public void ExactButEmptyIdDoesNotDisplaceUsableClosestCandidate()
        {
            StructureIdSelectionResult result = HeartStructureIdSelector.Select(
                Candidates(
                    Candidate("Heart", false),
                    Candidate("Heart_PRV", true)));

            Assert.IsTrue(result.IsSelected);
            Assert.AreEqual("Heart_PRV", result.SelectedStructureId);
            Assert.AreEqual(3, result.Candidates.Single(
                candidate => candidate.StructureId == "Heart_PRV").EditDistance.Value);
        }

        [TestMethod]
        public void UniqueClosestNormalizedIdWinsIndependentlyOfInputOrder()
        {
            List<StructureIdCandidate> forward = Candidates(
                Candidate("Heart_Avoidance", true),
                Candidate("Heart_PRV", true));

            StructureIdSelectionResult first = HeartStructureIdSelector.Select(forward);
            StructureIdSelectionResult second = HeartStructureIdSelector.Select(
                forward.AsEnumerable().Reverse());

            Assert.AreEqual("Heart_PRV", first.SelectedStructureId);
            Assert.AreEqual(first.SelectedStructureId, second.SelectedStructureId);
        }

        [TestMethod]
        public void EqualClosestDistanceIsUnavailable()
        {
            StructureIdSelectionResult result = HeartStructureIdSelector.Select(
                Candidates(
                    Candidate("Heart_A", true),
                    Candidate("Heart_B", true)));

            Assert.IsFalse(result.IsSelected);
            StringAssert.Contains(result.FailureReason, "ambiguous");
            StringAssert.Contains(result.FailureReason, "edit distance of 1");
        }

        [TestMethod]
        public void MultipleCaseInsensitiveExactIdsAreUnavailable()
        {
            StructureIdSelectionResult result = HeartStructureIdSelector.Select(
                Candidates(
                    Candidate("Heart", true),
                    Candidate("HEART", true)));

            Assert.IsFalse(result.IsSelected);
            StringAssert.Contains(result.FailureReason, "more than one");
        }

        [TestMethod]
        public void MissingOrOnlyEmptyMatchesAreUnavailableWithDifferentReasons()
        {
            StructureIdSelectionResult missing = HeartStructureIdSelector.Select(
                Candidates(Candidate("Cardiac", true)));
            StructureIdSelectionResult empty = HeartStructureIdSelector.Select(
                Candidates(Candidate("Heart", false)));

            Assert.IsFalse(missing.IsSelected);
            StringAssert.Contains(missing.FailureReason, "No structure ID");
            Assert.IsFalse(empty.IsSelected);
            StringAssert.Contains(empty.FailureReason, "all were empty");
        }

        private static StructureIdCandidate Candidate(string id, bool usable)
        {
            return new StructureIdCandidate(id, usable);
        }

        private static List<StructureIdCandidate> Candidates(
            params StructureIdCandidate[] candidates)
        {
            return candidates.ToList();
        }
    }
}
