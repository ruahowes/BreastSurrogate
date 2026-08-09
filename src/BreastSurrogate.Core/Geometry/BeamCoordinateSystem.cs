using System;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Geometry
{
    /// <summary>
    /// Orthonormal beam coordinate system at the isocentre plane.
    /// Patient-space positions and axes use DICOM coordinates in millimetres.
    /// </summary>
    /// <remarks>
    /// The unrotated axes are defined by
    /// u0 = normalize(referenceSuperior cross w) and v0 = w cross u0, where
    /// w points from source to isocentre. A positive collimator angle rotates
    /// u0 toward v0. This internal convention is not yet validated against
    /// Varian/Eclipse BEV signs.
    /// </remarks>
    public sealed class BeamCoordinateSystem
    {
        public BeamCoordinateSystem(
            VVector source,
            VVector isocentre,
            VVector referenceSuperior,
            double collimatorAngleDegrees)
        {
            ValidateFinite(source, "source");
            ValidateFinite(isocentre, "isocentre");
            ValidateFinite(referenceSuperior, "referenceSuperior");

            if (!IsFinite(collimatorAngleDegrees))
            {
                throw new ArgumentOutOfRangeException(
                    "collimatorAngleDegrees",
                    "Collimator angle must be finite.");
            }

            VVector wAxis = NormalizeRequired(
                isocentre - source,
                "isocentre",
                "Source and isocentre must define a non-zero finite central axis.");
            VVector superiorAxis = NormalizeRequired(
                referenceSuperior,
                "referenceSuperior",
                "Reference superior must be a non-zero finite vector.");
            VVector unrotatedUAxis = NormalizeRequired(
                VectorMath.Cross(superiorAxis, wAxis),
                "referenceSuperior",
                "Reference superior must not be parallel to the central axis.");
            VVector unrotatedVAxis = VectorMath.Cross(wAxis, unrotatedUAxis);

            // Reducing first avoids overflow for otherwise finite angle inputs.
            double angleRadians = (collimatorAngleDegrees % 360.0) * Math.PI / 180.0;
            double cosine = Math.Cos(angleRadians);
            double sine = Math.Sin(angleRadians);

            Source = source;
            Isocentre = isocentre;
            CollimatorAngleDegrees = collimatorAngleDegrees;
            WAxis = wAxis;
            UAxis = cosine * unrotatedUAxis + sine * unrotatedVAxis;
            VAxis = -sine * unrotatedUAxis + cosine * unrotatedVAxis;
        }

        public VVector Source { get; private set; }

        public VVector Isocentre { get; private set; }

        public double CollimatorAngleDegrees { get; private set; }

        /// <summary>
        /// Rotated beam-plane x axis.
        /// </summary>
        public VVector UAxis { get; private set; }

        /// <summary>
        /// Rotated beam-plane y axis.
        /// </summary>
        public VVector VAxis { get; private set; }

        /// <summary>
        /// Unit source-to-isocentre axis and isocentre-plane normal.
        /// </summary>
        public VVector WAxis { get; private set; }

        private static VVector NormalizeRequired(
            VVector vector,
            string parameterName,
            string message)
        {
            try
            {
                return VectorMath.Normalize(vector);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(message, parameterName, exception);
            }
        }

        private static void ValidateFinite(VVector vector, string parameterName)
        {
            if (!VectorMath.IsFinite(vector))
            {
                throw new ArgumentException("Vector components must be finite.", parameterName);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
