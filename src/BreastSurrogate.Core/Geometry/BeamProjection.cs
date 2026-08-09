using System;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Geometry
{
    /// <summary>
    /// Projects patient-space points along source rays onto a beam isocentre plane.
    /// </summary>
    public sealed class BeamProjection
    {
        /// <summary>
        /// Absolute denominator threshold used to reject rays parallel to the plane.
        /// The denominator is measured in millimetres because the plane normal is unit length.
        /// </summary>
        public const double DefaultParallelTolerance = 1e-12;

        public BeamProjection(BeamCoordinateSystem coordinateSystem)
        {
            if (coordinateSystem == null)
            {
                throw new ArgumentNullException("coordinateSystem");
            }

            CoordinateSystem = coordinateSystem;
        }

        public BeamCoordinateSystem CoordinateSystem { get; private set; }

        public ProjectedBeamPoint Project(VVector patientPoint)
        {
            if (!VectorMath.IsFinite(patientPoint))
            {
                throw new ArgumentException("Patient point components must be finite.", "patientPoint");
            }

            VVector sourceToPoint = patientPoint - CoordinateSystem.Source;
            double denominator = VectorMath.Dot(sourceToPoint, CoordinateSystem.WAxis);

            if (!IsFinite(denominator) || Math.Abs(denominator) <= DefaultParallelTolerance)
            {
                throw new InvalidOperationException(
                    "The source-to-point ray is parallel or too close to parallel to the isocentre plane.");
            }

            VVector sourceToIsocentre = CoordinateSystem.Isocentre - CoordinateSystem.Source;
            double numerator = VectorMath.Dot(sourceToIsocentre, CoordinateSystem.WAxis);
            double projectionParameter = numerator / denominator;

            if (!IsFinite(projectionParameter))
            {
                throw new InvalidOperationException("The projection parameter is not finite.");
            }

            VVector projectedPoint = CoordinateSystem.Source + projectionParameter * sourceToPoint;
            if (!VectorMath.IsFinite(projectedPoint))
            {
                throw new InvalidOperationException("The projected point is not finite.");
            }

            VVector isocentreOffset = projectedPoint - CoordinateSystem.Isocentre;
            double xBld = VectorMath.Dot(isocentreOffset, CoordinateSystem.UAxis);
            double yBld = VectorMath.Dot(isocentreOffset, CoordinateSystem.VAxis);

            if (!IsFinite(xBld) || !IsFinite(yBld))
            {
                throw new InvalidOperationException("The projected beam-plane coordinates are not finite.");
            }

            return new ProjectedBeamPoint(
                projectedPoint,
                xBld,
                yBld,
                projectionParameter);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
