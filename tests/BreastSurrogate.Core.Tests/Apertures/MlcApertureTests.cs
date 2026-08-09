using System;
using BreastSurrogate.Core.Apertures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Core.Tests.Apertures
{
    [TestClass]
    public class MlcApertureTests
    {
        [TestMethod]
        public void ContainsUsesSelectedLeafPairAndAsymmetricOpening()
        {
            MlcAperture aperture = CreateAperture();

            Assert.IsTrue(aperture.Contains(-2.0, 0.0), "Negative-X bank edge should be included.");
            Assert.IsTrue(aperture.Contains(6.0, 0.0), "Positive-X bank edge should be included.");
            Assert.IsTrue(aperture.Contains(4.0, 0.0));
            Assert.IsFalse(aperture.Contains(-2.001, 0.0));
            Assert.IsFalse(aperture.Contains(6.001, 0.0));
        }

        [TestMethod]
        public void ContainsUsesBankZeroAsNegativeXAndBankOneAsPositiveX()
        {
            MlcAperture aperture = CreateAperture();

            Assert.AreEqual(-2.0, aperture.GetNegativeXBankPositionMm(1), 1e-12);
            Assert.AreEqual(6.0, aperture.GetPositiveXBankPositionMm(1), 1e-12);
            Assert.IsTrue(aperture.Contains(-1.0, 0.0));
            Assert.IsFalse(aperture.Contains(-3.0, 0.0));
            Assert.IsTrue(aperture.Contains(5.0, 0.0));
            Assert.IsFalse(aperture.Contains(7.0, 0.0));
        }

        [TestMethod]
        public void ContainsReturnsFalseForEqualOrCrossedLeafTips()
        {
            MlcAperture aperture = CreateAperture();

            Assert.IsFalse(aperture.Contains(-5.0, -10.0), "Equal leaf tips are closed.");
            Assert.IsFalse(aperture.Contains(8.0, 10.0), "Crossed leaf tips are closed.");
        }

        [TestMethod]
        public void ContainsSelectsPositiveYLeafAtInternalBoundary()
        {
            MlcAperture aperture = CreateAperture();

            Assert.IsTrue(aperture.Contains(0.0, -5.0));
            Assert.IsFalse(aperture.Contains(-6.0, -5.0));
        }

        [TestMethod]
        public void ContainsReturnsFalseOutsideLeafSpan()
        {
            MlcAperture aperture = CreateAperture();

            Assert.IsFalse(aperture.Contains(0.0, -15.001));
            Assert.IsFalse(aperture.Contains(0.0, 15.001));
        }

        [TestMethod]
        public void ConstructorCopiesAndValidatesLeafArray()
        {
            MlcGeometryDefinition geometry = CreateGeometry();
            double[,] positions = CreateLeafPositions();
            var aperture = new MlcAperture(geometry, positions);
            positions[0, 1] = 100.0;

            Assert.AreEqual(-2.0, aperture.GetNegativeXBankPositionMm(1), 1e-12);
            Assert.ThrowsException<ArgumentException>(
                () => new MlcAperture(geometry, new double[2, 2]));
            Assert.ThrowsException<ArgumentException>(
                () => new MlcAperture(
                    geometry,
                    new[,] { { -1.0, double.NaN, -1.0 }, { 1.0, 1.0, 1.0 } }));
        }

        [TestMethod]
        public void ContainsRejectsNonFiniteCoordinates()
        {
            MlcAperture aperture = CreateAperture();

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => aperture.Contains(double.NaN, 0.0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => aperture.Contains(0.0, double.PositiveInfinity));
        }

        private static MlcAperture CreateAperture()
        {
            return new MlcAperture(CreateGeometry(), CreateLeafPositions());
        }

        private static MlcGeometryDefinition CreateGeometry()
        {
            return new MlcGeometryDefinition("Synthetic", new[] { -15.0, -5.0, 5.0, 15.0 });
        }

        private static double[,] CreateLeafPositions()
        {
            return new[,]
            {
                { -5.0, -2.0, 10.0 },
                { -5.0, 6.0, 7.0 }
            };
        }
    }
}
