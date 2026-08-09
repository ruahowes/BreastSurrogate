using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Geometry
{
    /// <summary>
    /// Result of projecting a patient-space point onto a beam isocentre plane.
    /// </summary>
    public sealed class ProjectedBeamPoint
    {
        public ProjectedBeamPoint(
            VVector projectedPoint,
            double xBld,
            double yBld,
            double projectionParameter)
        {
            ProjectedPoint = projectedPoint;
            XBld = xBld;
            YBld = yBld;
            ProjectionParameter = projectionParameter;
        }

        /// <summary>
        /// Projected DICOM patient-space point in millimetres.
        /// </summary>
        public VVector ProjectedPoint { get; private set; }

        /// <summary>
        /// Coordinate along the beam-plane u axis in millimetres.
        /// </summary>
        public double XBld { get; private set; }

        /// <summary>
        /// Coordinate along the beam-plane v axis in millimetres.
        /// </summary>
        public double YBld { get; private set; }

        /// <summary>
        /// Parameter t in Source + t * (Point - Source).
        /// </summary>
        public double ProjectionParameter { get; private set; }
    }
}
