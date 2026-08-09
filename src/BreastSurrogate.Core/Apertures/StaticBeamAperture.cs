using System;
using BreastSurrogate.Core.Geometry;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Apertures
{
    /// <summary>
    /// Jaw-only static field classifier for patient-space points.
    /// </summary>
    public sealed class StaticBeamAperture
    {
        public StaticBeamAperture(BeamProjection projection, JawAperture jaws)
        {
            if (projection == null)
            {
                throw new ArgumentNullException("projection");
            }

            if (jaws == null)
            {
                throw new ArgumentNullException("jaws");
            }

            Projection = projection;
            Jaws = jaws;
        }

        public BeamProjection Projection { get; private set; }

        public JawAperture Jaws { get; private set; }

        /// <summary>
        /// Projects a DICOM patient-space point and applies the inclusive jaw opening.
        /// </summary>
        public bool Contains(VVector patientPoint)
        {
            ProjectedBeamPoint projectedPoint = Projection.Project(patientPoint);
            return Jaws.Contains(projectedPoint.XBld, projectedPoint.YBld);
        }
    }
}
