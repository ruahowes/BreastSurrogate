using System;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Apertures
{
    /// <summary>
    /// Inclusive rectangular jaw opening in the BLD isocentre plane, in millimetres.
    /// </summary>
    public sealed class JawAperture
    {
        public JawAperture(VRect<double> bounds)
        {
            ValidateFinite(bounds.X1, "bounds", "X1");
            ValidateFinite(bounds.Y1, "bounds", "Y1");
            ValidateFinite(bounds.X2, "bounds", "X2");
            ValidateFinite(bounds.Y2, "bounds", "Y2");

            if (bounds.X1 > bounds.X2)
            {
                throw new ArgumentException("Jaw bounds require X1 to be less than or equal to X2.", "bounds");
            }

            if (bounds.Y1 > bounds.Y2)
            {
                throw new ArgumentException("Jaw bounds require Y1 to be less than or equal to Y2.", "bounds");
            }

            Bounds = bounds;
        }

        public VRect<double> Bounds { get; private set; }

        /// <summary>
        /// Returns whether the finite BLD point lies inside or on the jaw boundary.
        /// </summary>
        public bool Contains(double xBld, double yBld)
        {
            if (!IsFinite(xBld))
            {
                throw new ArgumentOutOfRangeException("xBld", "BLD coordinate must be finite.");
            }

            if (!IsFinite(yBld))
            {
                throw new ArgumentOutOfRangeException("yBld", "BLD coordinate must be finite.");
            }

            return xBld >= Bounds.X1
                && xBld <= Bounds.X2
                && yBld >= Bounds.Y1
                && yBld <= Bounds.Y2;
        }

        private static void ValidateFinite(double value, string parameterName, string coordinateName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentException(
                    "Jaw coordinate " + coordinateName + " must be finite.",
                    parameterName);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
