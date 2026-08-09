using System;
using BreastSurrogate.Core.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Tests.Geometry
{
    [TestClass]
    public class VectorMathTests
    {
        private const double AssertionTolerance = 1e-12;

        [TestMethod]
        public void VVectorAdditionAndSubtractionHaveExpectedComponents()
        {
            var left = new VVector(1.0, -2.0, 3.5);
            var right = new VVector(4.0, 5.0, -0.5);

            VVector sum = left + right;
            VVector difference = left - right;

            AssertVector(5.0, 3.0, 3.0, sum);
            AssertVector(-3.0, -7.0, 4.0, difference);
        }

        [TestMethod]
        public void DotReturnsScalarProduct()
        {
            var left = new VVector(1.0, 2.0, 3.0);
            var right = new VVector(4.0, -5.0, 6.0);

            double result = VectorMath.Dot(left, right);

            Assert.AreEqual(12.0, result, AssertionTolerance);
        }

        [TestMethod]
        public void CrossUsesRightHandedOrientation()
        {
            var xAxis = new VVector(1.0, 0.0, 0.0);
            var yAxis = new VVector(0.0, 1.0, 0.0);

            VVector result = VectorMath.Cross(xAxis, yAxis);

            AssertVector(0.0, 0.0, 1.0, result);
        }

        [TestMethod]
        public void CrossIsOrthogonalToBothInputs()
        {
            var left = new VVector(2.0, -3.0, 4.0);
            var right = new VVector(-1.0, 5.0, 2.0);

            VVector result = VectorMath.Cross(left, right);

            Assert.AreEqual(0.0, VectorMath.Dot(result, left), AssertionTolerance);
            Assert.AreEqual(0.0, VectorMath.Dot(result, right), AssertionTolerance);
        }

        [TestMethod]
        public void NormalizeReturnsUnitCopyWithoutChangingInput()
        {
            var input = new VVector(3.0, 4.0, 0.0);

            VVector result = VectorMath.Normalize(input);

            AssertVector(0.6, 0.8, 0.0, result);
            Assert.AreEqual(1.0, result.Length, AssertionTolerance);
            AssertVector(3.0, 4.0, 0.0, input);
        }

        [TestMethod]
        public void NormalizeRejectsZeroLengthVector()
        {
            Assert.ThrowsException<ArgumentException>(
                () => VectorMath.Normalize(new VVector(0.0, 0.0, 0.0)));
        }

        [TestMethod]
        public void NormalizeRejectsVectorAtConfiguredZeroLengthTolerance()
        {
            Assert.ThrowsException<ArgumentException>(
                () => VectorMath.Normalize(new VVector(0.01, 0.0, 0.0), 0.01));
        }

        [TestMethod]
        public void IsFiniteRejectsNaNAndInfinityInAnyComponent()
        {
            Assert.IsTrue(VectorMath.IsFinite(new VVector(1.0, 2.0, 3.0)));
            Assert.IsFalse(VectorMath.IsFinite(new VVector(double.NaN, 2.0, 3.0)));
            Assert.IsFalse(VectorMath.IsFinite(new VVector(1.0, double.PositiveInfinity, 3.0)));
            Assert.IsFalse(VectorMath.IsFinite(new VVector(1.0, 2.0, double.NegativeInfinity)));
        }

        [TestMethod]
        public void AreApproximatelyEqualUsesInclusiveAbsoluteTolerancePerComponent()
        {
            var expected = new VVector(10.0, -20.0, 30.0);
            var atTolerance = new VVector(10.125, -20.125, 30.125);
            var outsideTolerance = new VVector(10.1251, -20.0, 30.0);

            Assert.IsTrue(VectorMath.AreApproximatelyEqual(expected, atTolerance, 0.125));
            Assert.IsFalse(VectorMath.AreApproximatelyEqual(expected, outsideTolerance, 0.125));
        }

        [TestMethod]
        public void AreApproximatelyEqualReturnsFalseForNonFiniteVector()
        {
            var finite = new VVector(1.0, 2.0, 3.0);
            var nonFinite = new VVector(1.0, double.NaN, 3.0);

            Assert.IsFalse(VectorMath.AreApproximatelyEqual(finite, nonFinite, 0.1));
        }

        [TestMethod]
        public void ToleranceOperationsRejectInvalidTolerance()
        {
            var vector = new VVector(1.0, 0.0, 0.0);

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => VectorMath.AreApproximatelyEqual(vector, vector, -0.1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => VectorMath.AreApproximatelyEqual(vector, vector, double.NaN));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => VectorMath.Normalize(vector, double.PositiveInfinity));
        }

        private static void AssertVector(double x, double y, double z, VVector actual)
        {
            Assert.AreEqual(x, actual.x, AssertionTolerance, "Unexpected x component.");
            Assert.AreEqual(y, actual.y, AssertionTolerance, "Unexpected y component.");
            Assert.AreEqual(z, actual.z, AssertionTolerance, "Unexpected z component.");
        }
    }
}
