using System;
using BreastSurrogate.Core.Geometry;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Apertures
{
    /// <summary>
    /// Static jaw and optional MLC field classifier for patient-space points.
    /// </summary>
    public sealed class StaticBeamAperture
    {
        public StaticBeamAperture(BeamProjection projection, JawAperture jaws)
            : this(projection, jaws, null)
        {
        }

        public StaticBeamAperture(
            BeamProjection projection,
            JawAperture jaws,
            MlcAperture mlc)
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
            Mlc = mlc;
        }

        public BeamProjection Projection { get; private set; }

        public JawAperture Jaws { get; private set; }

        public MlcAperture Mlc { get; private set; }

        /// <summary>
        /// Projects a DICOM patient-space point and applies the jaw opening and,
        /// when present, the static MLC opening.
        /// </summary>
        public bool Contains(VVector patientPoint)
        {
            ProjectedBeamPoint projectedPoint = Projection.Project(patientPoint);
            return Jaws.Contains(projectedPoint.XBld, projectedPoint.YBld)
                && (Mlc == null || Mlc.Contains(projectedPoint.XBld, projectedPoint.YBld));
        }
    }
}
