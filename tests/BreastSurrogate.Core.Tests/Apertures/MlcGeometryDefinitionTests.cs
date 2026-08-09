using System;
using BreastSurrogate.Core.Apertures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Core.Tests.Apertures
{
    [TestClass]
    public class MlcGeometryDefinitionTests
    {
        [TestMethod]
        public void Millennium120HasExpectedModelSpanAndPairCount()
        {
            MlcGeometryDefinition geometry = SupportedMlcGeometries.Millennium120;

            Assert.AreEqual("Millennium 120", geometry.ModelIdentifier);
            Assert.AreEqual(60, geometry.LeafPairCount);
            Assert.AreEqual(-200.0, geometry.MinimumYBldMm, 1e-12);
            Assert.AreEqual(200.0, geometry.MaximumYBldMm, 1e-12);
        }

        [TestMethod]
        public void Millennium120HasExpectedWidthForEveryLeafPair()
        {
            MlcGeometryDefinition geometry = SupportedMlcGeometries.Millennium120;

            for (int leafPairIndex = 0; leafPairIndex < geometry.LeafPairCount; leafPairIndex++)
            {
                double expectedWidthMm = leafPairIndex < 10 || leafPairIndex >= 50
                    ? 10.0
                    : 5.0;
                double actualWidthMm = geometry.GetLeafUpperBoundaryMm(leafPairIndex)
                    - geometry.GetLeafLowerBoundaryMm(leafPairIndex);

                Assert.AreEqual(
                    expectedWidthMm,
                    actualWidthMm,
                    1e-12,
                    "Unexpected width for leaf pair " + leafPairIndex + ".");
            }
        }

        [TestMethod]
        public void TryGetLeafPairIndexSelectsEveryLeafAtItsMidpoint()
        {
            MlcGeometryDefinition geometry = SupportedMlcGeometries.Millennium120;

            for (int expectedIndex = 0; expectedIndex < geometry.LeafPairCount; expectedIndex++)
            {
                double midpoint = (geometry.GetLeafLowerBoundaryMm(expectedIndex)
                    + geometry.GetLeafUpperBoundaryMm(expectedIndex)) / 2.0;
                int actualIndex;

                Assert.IsTrue(geometry.TryGetLeafPairIndex(midpoint, out actualIndex));
                Assert.AreEqual(expectedIndex, actualIndex);
            }
        }

        [TestMethod]
        public void TryGetLeafPairIndexUsesDocumentedBoundaryConvention()
        {
            MlcGeometryDefinition geometry = SupportedMlcGeometries.Millennium120;
            int leafPairIndex;

            Assert.IsTrue(geometry.TryGetLeafPairIndex(-200.0, out leafPairIndex));
            Assert.AreEqual(0, leafPairIndex);
            Assert.IsTrue(geometry.TryGetLeafPairIndex(-190.0, out leafPairIndex));
            Assert.AreEqual(1, leafPairIndex);
            Assert.IsTrue(geometry.TryGetLeafPairIndex(-100.0, out leafPairIndex));
            Assert.AreEqual(10, leafPairIndex);
            Assert.IsTrue(geometry.TryGetLeafPairIndex(100.0, out leafPairIndex));
            Assert.AreEqual(50, leafPairIndex);
            Assert.IsTrue(geometry.TryGetLeafPairIndex(200.0, out leafPairIndex));
            Assert.AreEqual(59, leafPairIndex);
        }

        [TestMethod]
        public void TryGetLeafPairIndexRejectsPointsOutsidePhysicalSpan()
        {
            MlcGeometryDefinition geometry = SupportedMlcGeometries.Millennium120;
            int leafPairIndex;

            Assert.IsFalse(geometry.TryGetLeafPairIndex(-200.001, out leafPairIndex));
            Assert.AreEqual(-1, leafPairIndex);
            Assert.IsFalse(geometry.TryGetLeafPairIndex(200.001, out leafPairIndex));
            Assert.AreEqual(-1, leafPairIndex);
        }

        [TestMethod]
        public void ConstructorCopiesAndValidatesBoundaries()
        {
            var boundaries = new[] { -10.0, 0.0, 10.0 };
            var geometry = new MlcGeometryDefinition("Synthetic", boundaries);
            boundaries[1] = 7.0;

            Assert.AreEqual(0.0, geometry.GetLeafUpperBoundaryMm(0), 1e-12);
            Assert.ThrowsException<ArgumentException>(
                () => new MlcGeometryDefinition("Synthetic", new[] { -10.0, -10.0, 10.0 }));
            Assert.ThrowsException<ArgumentException>(
                () => new MlcGeometryDefinition("Synthetic", new[] { -10.0, double.NaN }));
        }

        [TestMethod]
        public void TryGetLeafPairIndexRejectsNonFiniteCoordinate()
        {
            int leafPairIndex;

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => SupportedMlcGeometries.Millennium120.TryGetLeafPairIndex(
                    double.NaN,
                    out leafPairIndex));
        }
    }
}
