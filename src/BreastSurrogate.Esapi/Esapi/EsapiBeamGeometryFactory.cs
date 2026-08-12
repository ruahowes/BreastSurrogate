using System;
using System.Collections.Generic;
using System.Linq;
using BreastSurrogate.Core.Apertures;
using BreastSurrogate.Core.Geometry;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// Creates a validated static jaw-only or jaw-and-MLC Core aperture from a real ESAPI beam.
    /// </summary>
    public sealed class EsapiBeamGeometryFactory
    {
        public const double AngleToleranceDegrees = 0.01;
        public const double PositionToleranceMm = 0.01;

        private const string UnsupportedTreatmentUnitId = "HalcyonE";

        private static readonly VVector DicomPatientSuperior = new VVector(0.0, 0.0, 1.0);

        public StaticBeamAperture Create(Beam beam, PatientOrientation patientOrientation)
        {
            if (beam == null)
            {
                throw new ArgumentNullException("beam");
            }

            ValidateBeamType(beam, patientOrientation);
            bool usesMlc = UsesStaticMlc(beam.MLCPlanType);
            MlcGeometryDefinition mlcGeometry = usesMlc
                ? GetSupportedMlcGeometry(beam)
                : null;

            List<ControlPoint> controlPoints = beam.ControlPoints.ToList();
            if (controlPoints.Count == 0)
            {
                throw Unsupported(beam, "has no control points");
            }

            ControlPoint firstControlPoint = controlPoints[0];
            ValidateControlPointValues(beam, firstControlPoint, 0, mlcGeometry);
            ValidateControlPointConstancy(
                beam,
                firstControlPoint,
                controlPoints,
                mlcGeometry);

            try
            {
                VVector source = beam.GetSourceLocation(firstControlPoint.GantryAngle);
                var coordinateSystem = new BeamCoordinateSystem(
                    source,
                    beam.IsocenterPosition,
                    DicomPatientSuperior,
                    firstControlPoint.CollimatorAngle);
                var projection = new BeamProjection(coordinateSystem);
                var jaws = new JawAperture(firstControlPoint.JawPositions);
                MlcAperture mlc = usesMlc
                    ? new MlcAperture(
                        mlcGeometry,
                        ConvertLeafPositions(firstControlPoint.LeafPositions))
                    : null;
                return new StaticBeamAperture(projection, jaws, mlc);
            }
            catch (ArgumentException exception)
            {
                throw Unsupported(
                    beam,
                    "contains invalid source, isocentre, beam-axis, collimator, jaw, or MLC geometry",
                    exception);
            }
        }

        private static void ValidateBeamType(Beam beam, PatientOrientation patientOrientation)
        {
            if (patientOrientation != PatientOrientation.HeadFirstSupine)
            {
                throw Unsupported(
                    beam,
                    "requires head-first supine orientation; actual orientation is "
                    + patientOrientation);
            }

            if (beam.IsSetupField)
            {
                throw Unsupported(beam, "is a setup field");
            }

            if (beam.IsImagingTreatmentField)
            {
                throw Unsupported(beam, "is an imaging treatment field");
            }

            if (!(beam.Plan is ExternalPlanSetup))
            {
                throw Unsupported(beam, "does not belong to an external beam plan");
            }

            if (string.IsNullOrWhiteSpace(beam.EnergyModeDisplayName)
                || beam.EnergyModeDisplayName.IndexOf("X", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw Unsupported(
                    beam,
                    "does not have a supported photon energy mode; actual mode is '"
                    + (beam.EnergyModeDisplayName ?? "<null>")
                    + "'");
            }

            if (!IsFinite(beam.ArcLength) || Math.Abs(beam.ArcLength) > AngleToleranceDegrees)
            {
                throw Unsupported(
                    beam,
                    "is not static; arc length is "
                    + beam.ArcLength
                    + " degrees");
            }

            if (!IsSupportedMlcPlanType(beam.MLCPlanType))
            {
                throw Unsupported(
                    beam,
                    "does not use a static MLC plan type; actual type is "
                    + beam.MLCPlanType);
            }

            if (beam.TreatmentUnit != null
                && string.Equals(
                    beam.TreatmentUnit.Id,
                    UnsupportedTreatmentUnitId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw Unsupported(beam, "uses unsupported Halcyon geometry");
            }
        }

        internal static bool UsesStaticMlc(MLCPlanType planType)
        {
            return planType == MLCPlanType.Static;
        }

        internal static bool IsSupportedMlcPlanType(MLCPlanType planType)
        {
            return planType == MLCPlanType.Static
                || planType == MLCPlanType.NotDefined;
        }

        private static void ValidateControlPointConstancy(
            Beam beam,
            ControlPoint reference,
            IList<ControlPoint> controlPoints,
            MlcGeometryDefinition mlcGeometry)
        {
            for (int index = 1; index < controlPoints.Count; index++)
            {
                ControlPoint candidate = controlPoints[index];
                ValidateControlPointValues(beam, candidate, index, mlcGeometry);

                if (!AnglesEqual(reference.GantryAngle, candidate.GantryAngle))
                {
                    throw ChangingControlPoint(beam, index, "gantry angle");
                }

                if (!AnglesEqual(reference.CollimatorAngle, candidate.CollimatorAngle))
                {
                    throw ChangingControlPoint(beam, index, "collimator angle");
                }

                if (!AnglesEqual(reference.PatientSupportAngle, candidate.PatientSupportAngle))
                {
                    throw ChangingControlPoint(beam, index, "patient support angle");
                }

                if (!RectanglesEqual(reference.JawPositions, candidate.JawPositions))
                {
                    throw ChangingControlPoint(beam, index, "jaw positions");
                }

                if (mlcGeometry != null
                    && !LeafPositionsEqual(reference.LeafPositions, candidate.LeafPositions))
                {
                    throw ChangingControlPoint(beam, index, "leaf positions");
                }
            }
        }

        private static void ValidateControlPointValues(
            Beam beam,
            ControlPoint controlPoint,
            int index,
            MlcGeometryDefinition mlcGeometry)
        {
            if (!IsFinite(controlPoint.GantryAngle)
                || !IsFinite(controlPoint.CollimatorAngle)
                || !IsFinite(controlPoint.PatientSupportAngle))
            {
                throw Unsupported(
                    beam,
                    "has non-finite angles at control point " + index);
            }

            if (!AngleIsZero(controlPoint.PatientSupportAngle))
            {
                throw Unsupported(
                    beam,
                    "has non-zero patient support angle at control point "
                    + index
                    + "; actual angle is "
                    + controlPoint.PatientSupportAngle
                    + " degrees");
            }

            VRect<double> jaws = controlPoint.JawPositions;
            if (!IsFinite(jaws.X1)
                || !IsFinite(jaws.Y1)
                || !IsFinite(jaws.X2)
                || !IsFinite(jaws.Y2))
            {
                throw Unsupported(
                    beam,
                    "has non-finite jaw positions at control point " + index);
            }

            if (mlcGeometry == null)
            {
                return;
            }

            float[,] leaves = controlPoint.LeafPositions;
            if (leaves == null
                || leaves.GetLength(0) != 2
                || leaves.GetLength(1) != mlcGeometry.LeafPairCount)
            {
                string dimensions = leaves == null
                    ? "<null>"
                    : leaves.GetLength(0) + " x " + leaves.GetLength(1);
                throw Unsupported(
                    beam,
                    "has leaf-array dimensions "
                    + dimensions
                    + " at control point "
                    + index
                    + "; model '"
                    + mlcGeometry.ModelIdentifier
                    + "' requires 2 x "
                    + mlcGeometry.LeafPairCount);
            }

            for (int bank = 0; bank < leaves.GetLength(0); bank++)
            {
                for (int leaf = 0; leaf < leaves.GetLength(1); leaf++)
                {
                    if (float.IsNaN(leaves[bank, leaf]) || float.IsInfinity(leaves[bank, leaf]))
                    {
                        throw Unsupported(
                            beam,
                            "has non-finite leaf positions at control point " + index);
                    }
                }
            }
        }

        private static MlcGeometryDefinition GetSupportedMlcGeometry(Beam beam)
        {
            if (beam.MLC == null)
            {
                throw Unsupported(beam, "has no MLC hardware description");
            }

            MlcGeometryDefinition geometry = SupportedMlcGeometries.Millennium120;
            if (!string.Equals(
                beam.MLC.Model,
                geometry.ModelIdentifier,
                StringComparison.Ordinal))
            {
                throw Unsupported(
                    beam,
                    "uses unsupported MLC model '"
                    + (beam.MLC.Model ?? "<null>")
                    + "'; the only configured model is '"
                    + geometry.ModelIdentifier
                    + "'");
            }

            return geometry;
        }

        private static double[,] ConvertLeafPositions(float[,] leafPositions)
        {
            int bankCount = leafPositions.GetLength(0);
            int leafPairCount = leafPositions.GetLength(1);
            var converted = new double[bankCount, leafPairCount];

            for (int bankIndex = 0; bankIndex < bankCount; bankIndex++)
            {
                for (int leafPairIndex = 0; leafPairIndex < leafPairCount; leafPairIndex++)
                {
                    converted[bankIndex, leafPairIndex] =
                        leafPositions[bankIndex, leafPairIndex];
                }
            }

            return converted;
        }

        private static bool AnglesEqual(double left, double right)
        {
            return AngularDifference(left, right) <= AngleToleranceDegrees;
        }

        private static bool AngleIsZero(double angle)
        {
            return AngularDifference(angle, 0.0) <= AngleToleranceDegrees;
        }

        private static double AngularDifference(double left, double right)
        {
            double difference = Math.Abs((left - right) % 360.0);
            return Math.Min(difference, 360.0 - difference);
        }

        private static bool RectanglesEqual(VRect<double> left, VRect<double> right)
        {
            return PositionsEqual(left.X1, right.X1)
                && PositionsEqual(left.Y1, right.Y1)
                && PositionsEqual(left.X2, right.X2)
                && PositionsEqual(left.Y2, right.Y2);
        }

        private static bool LeafPositionsEqual(float[,] left, float[,] right)
        {
            if (left.GetLength(0) != right.GetLength(0)
                || left.GetLength(1) != right.GetLength(1))
            {
                return false;
            }

            for (int bank = 0; bank < left.GetLength(0); bank++)
            {
                for (int leaf = 0; leaf < left.GetLength(1); leaf++)
                {
                    if (!PositionsEqual(left[bank, leaf], right[bank, leaf]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool PositionsEqual(double left, double right)
        {
            return Math.Abs(left - right) <= PositionToleranceMm;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static UnsupportedBeamGeometryException ChangingControlPoint(
            Beam beam,
            int controlPointIndex,
            string propertyName)
        {
            return Unsupported(
                beam,
                "changes "
                + propertyName
                + " at control point "
                + controlPointIndex
                + " and therefore does not have one static aperture");
        }

        private static UnsupportedBeamGeometryException Unsupported(Beam beam, string reason)
        {
            return new UnsupportedBeamGeometryException(
                "Beam '" + beam.Id + "' is unsupported because it " + reason + ".");
        }

        private static UnsupportedBeamGeometryException Unsupported(
            Beam beam,
            string reason,
            Exception innerException)
        {
            return new UnsupportedBeamGeometryException(
                "Beam '" + beam.Id + "' is unsupported because it " + reason + ".",
                innerException);
        }
    }
}
