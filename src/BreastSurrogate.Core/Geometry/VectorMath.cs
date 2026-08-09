using System;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Geometry
{
    /// <summary>
    /// Deterministic vector operations used by the Core geometry calculations.
    /// </summary>
    public static class VectorMath
    {
        /// <summary>
        /// Default length threshold below which a vector is treated as zero-length.
        /// </summary>
        public const double DefaultZeroLengthTolerance = 1e-12;

        /// <summary>
        /// Returns the scalar product of two vectors.
        /// </summary>
        public static double Dot(VVector left, VVector right)
        {
            return left.ScalarProduct(right);
        }

        /// <summary>
        /// Returns the standard right-handed cross product <paramref name="left"/>
        /// cross <paramref name="right"/>.
        /// </summary>
        public static VVector Cross(VVector left, VVector right)
        {
            return new VVector(
                left.y * right.z - left.z * right.y,
                left.z * right.x - left.x * right.z,
                left.x * right.y - left.y * right.x);
        }

        /// <summary>
        /// Returns a unit-length copy of <paramref name="vector"/>.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when the vector is non-finite or has length at or below the supplied tolerance.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="zeroLengthTolerance"/> is negative or non-finite.
        /// </exception>
        public static VVector Normalize(
            VVector vector,
            double zeroLengthTolerance = DefaultZeroLengthTolerance)
        {
            ValidateTolerance(zeroLengthTolerance, "zeroLengthTolerance");

            if (!IsFinite(vector))
            {
                throw new ArgumentException("Vector components must be finite.", "vector");
            }

            double length = vector.Length;
            if (!IsFinite(length) || length <= zeroLengthTolerance)
            {
                throw new ArgumentException(
                    "Vector length must be finite and greater than the zero-length tolerance.",
                    "vector");
            }

            return vector / length;
        }

        /// <summary>
        /// Returns whether every vector component is finite.
        /// </summary>
        public static bool IsFinite(VVector vector)
        {
            return IsFinite(vector.x) && IsFinite(vector.y) && IsFinite(vector.z);
        }

        /// <summary>
        /// Returns whether corresponding finite components differ by no more than
        /// the supplied absolute tolerance.
        /// </summary>
        public static bool AreApproximatelyEqual(
            VVector left,
            VVector right,
            double tolerance)
        {
            ValidateTolerance(tolerance, "tolerance");

            if (!IsFinite(left) || !IsFinite(right))
            {
                return false;
            }

            return Math.Abs(left.x - right.x) <= tolerance
                && Math.Abs(left.y - right.y) <= tolerance
                && Math.Abs(left.z - right.z) <= tolerance;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void ValidateTolerance(double tolerance, string parameterName)
        {
            if (!IsFinite(tolerance) || tolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Tolerance must be finite and non-negative.");
            }
        }
    }
}
