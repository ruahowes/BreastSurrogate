using System;
using BreastSurrogate.Core.Apertures;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Tests.Apertures
{
    [TestClass]
    public class JawApertureTests
    {
        [TestMethod]
        public void ContainsReturnsTrueAtCentre()
        {
            JawAperture jaws = CreateJaws();

            Assert.IsTrue(jaws.Contains(0.0, 0.0));
        }

        [TestMethod]
        public void ContainsReturnsFalseBeyondEachJaw()
        {
            JawAperture jaws = CreateJaws();

            Assert.IsFalse(jaws.Contains(-10.001, 0.0), "Point beyond X1 should be outside.");
            Assert.IsFalse(jaws.Contains(20.001, 0.0), "Point beyond X2 should be outside.");
            Assert.IsFalse(jaws.Contains(0.0, -30.001), "Point beyond Y1 should be outside.");
            Assert.IsFalse(jaws.Contains(0.0, 40.001), "Point beyond Y2 should be outside.");
        }

        [TestMethod]
        public void ContainsIncludesEveryJawBoundary()
        {
            JawAperture jaws = CreateJaws();

            Assert.IsTrue(jaws.Contains(-10.0, 0.0), "X1 boundary should be included.");
            Assert.IsTrue(jaws.Contains(20.0, 0.0), "X2 boundary should be included.");
            Assert.IsTrue(jaws.Contains(0.0, -30.0), "Y1 boundary should be included.");
            Assert.IsTrue(jaws.Contains(0.0, 40.0), "Y2 boundary should be included.");
            Assert.IsTrue(jaws.Contains(-10.0, -30.0), "Jaw corner should be included.");
            Assert.IsTrue(jaws.Contains(20.0, 40.0), "Opposite jaw corner should be included.");
        }

        [TestMethod]
        public void ContainsIncludesFloatingPointResidueAtZeroJawBoundaries()
        {
            const double projectionResidueMm = 2.6e-14;
            JawAperture upperZeroJaw = new JawAperture(new VRect<double>(-120.0, -98.0, 0.0, 83.0));
            JawAperture lowerZeroJaw = new JawAperture(new VRect<double>(0.0, -99.0, 120.0, 91.0));

            Assert.IsTrue(
                upperZeroJaw.Contains(projectionResidueMm, 0.0),
                "Positive projection residue at an X2=0 boundary should be included.");
            Assert.IsTrue(
                lowerZeroJaw.Contains(-projectionResidueMm, 0.0),
                "Negative projection residue at an X1=0 boundary should be included.");
        }

        [TestMethod]
        public void ContainsDoesNotIncludePointMateriallyBeyondBoundary()
        {
            JawAperture jaws = CreateJaws();

            Assert.IsFalse(jaws.Contains(20.000001, 0.0));
        }

        [TestMethod]
        public void ConstructorRejectsReversedBounds()
        {
            Assert.ThrowsException<ArgumentException>(
                () => new JawAperture(new VRect<double>(1.0, -2.0, -1.0, 2.0)));
            Assert.ThrowsException<ArgumentException>(
                () => new JawAperture(new VRect<double>(-1.0, 2.0, 1.0, -2.0)));
        }

        [TestMethod]
        public void ConstructorRejectsNonFiniteBounds()
        {
            Assert.ThrowsException<ArgumentException>(
                () => new JawAperture(new VRect<double>(double.NaN, -2.0, 1.0, 2.0)));
            Assert.ThrowsException<ArgumentException>(
                () => new JawAperture(new VRect<double>(-1.0, -2.0, double.PositiveInfinity, 2.0)));
        }

        [TestMethod]
        public void ContainsRejectsNonFinitePoint()
        {
            JawAperture jaws = CreateJaws();

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => jaws.Contains(double.NaN, 0.0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => jaws.Contains(0.0, double.NegativeInfinity));
        }

        private static JawAperture CreateJaws()
        {
            return new JawAperture(new VRect<double>(-10.0, -30.0, 20.0, 40.0));
        }
    }
}
