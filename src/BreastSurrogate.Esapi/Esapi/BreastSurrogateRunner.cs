using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using BreastSurrogate.Core.Apertures;
using BreastSurrogate.Core.Geometry;
using Uclh.XRT.Esapi.Core;
using Uclh.XRT.Esapi.Utilities;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// Read-only Phase 4 shell for inspecting the active Eclipse context.
    /// </summary>
    public sealed class BreastSurrogateRunner
    {
        private const string ApplicationName = "BreastSurrogate";
        private const string UnknownPatientId = "NoPatient";

        private readonly string _logDirectory;

        public BreastSurrogateRunner(string logDirectory)
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                throw new ArgumentException("Log directory cannot be null or empty.", "logDirectory");
            }

            _logDirectory = logDirectory;
        }

        public void Run(ScriptContext scriptContext)
        {
            var logger = new Logger(_logDirectory, ApplicationName, true);
            var stopwatch = Stopwatch.StartNew();
            string patientId = UnknownPatientId;

            try
            {
                logger.LogMethodStart();

                string validationError = ValidateContext(scriptContext);
                if (validationError != null)
                {
                    if (scriptContext != null && scriptContext.Patient != null)
                    {
                        patientId = scriptContext.Patient.Id;
                    }

                    ReportFailure(logger, stopwatch, patientId, validationError);
                    return;
                }

                patientId = scriptContext.Patient.Id;
                var context = new EsapiContext(scriptContext);

                LogContext(logger, context);

                List<Beam> treatmentBeams = BeamAnalyzer
                    .GetTreatmentBeams(context.ps)
                    .ToList();
                logger.Log("TreatmentBeamCount", treatmentBeams.Count);

                if (treatmentBeams.Count == 0)
                {
                    ReportFailure(
                        logger,
                        stopwatch,
                        patientId,
                        "The open plan contains no treatment beams.");
                    return;
                }

                var factory = new EsapiBeamGeometryFactory();
                for (int beamIndex = 0; beamIndex < treatmentBeams.Count; beamIndex++)
                {
                    Beam beam = treatmentBeams[beamIndex];
                    LogBeam(logger, beam, beamIndex);

                    StaticBeamAperture aperture = factory.Create(
                        beam,
                        context.im.ImagingOrientation);
                    LogCoreGeometry(logger, aperture, beamIndex);
                }

                stopwatch.Stop();
                logger.LogTiming("Phase5BeamGeometryInspection", stopwatch.ElapsedMilliseconds);
                logger.Log("Status", "Success");

                string logError = TryWriteLog(logger, patientId);
                if (logError != null)
                {
                    MessageBox.Show(
                        "BreastSurrogate inspected " + treatmentBeams.Count
                        + " treatment beam(s), but the debug log could not be written.\n\n"
                        + logError,
                        ApplicationName,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                MessageBox.Show(
                    "BreastSurrogate Phase 5 jaw-geometry validation completed for "
                    + treatmentBeams.Count
                    + " treatment beam(s).\n\nDebug log directory:\n"
                    + _logDirectory,
                    ApplicationName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (UnsupportedBeamGeometryException exception)
            {
                ReportFailure(logger, stopwatch, patientId, exception.Message);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                logger.Log("Status", "Unexpected failure");
                logger.Log("ExceptionType", exception.GetType().FullName);
                logger.Log("ExceptionMessage", exception.Message);
                logger.LogTiming("Phase5BeamGeometryInspection", stopwatch.ElapsedMilliseconds);

                string logError = TryWriteLog(logger, patientId);
                string logMessage = logError == null
                    ? "\n\nA debug log was written to:\n" + _logDirectory
                    : "\n\nThe debug log could not be written:\n" + logError;

                MessageBox.Show(
                    "BreastSurrogate inspection failed.\n\n"
                    + exception.Message
                    + logMessage,
                    ApplicationName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string ValidateContext(ScriptContext scriptContext)
        {
            if (scriptContext == null)
            {
                return "Eclipse did not provide a script context.";
            }

            if (scriptContext.Patient == null)
            {
                return "No patient is open. Open a patient before running BreastSurrogate.";
            }

            if (scriptContext.PlanSetup == null)
            {
                return "No plan is open. Open a treatment plan before running BreastSurrogate.";
            }

            if (scriptContext.StructureSet == null)
            {
                return "The open plan has no associated structure set.";
            }

            if (scriptContext.Image == null)
            {
                return "The open plan has no associated 3D image.";
            }

            return null;
        }

        private static void LogContext(Logger logger, EsapiContext context)
        {
            logger.Log("PatientId", context.pa.Id);
            logger.Log("PlanId", context.ps.Id);
            logger.Log("StructureSetId", context.ss.Id);
            logger.Log("ImageId", context.im.Id);
            logger.Log("ImageOrientation", context.im.ImagingOrientation);
            logger.Log(
                "ImageSizeVoxels",
                FormatDimensions(context.im.XSize, context.im.YSize, context.im.ZSize));
            logger.Log(
                "ImageResolutionMm",
                FormatDimensions(context.im.XRes, context.im.YRes, context.im.ZRes));
            logger.Log("UserOriginDicomMm", FormatVector(context.uo));
        }

        private static void LogBeam(Logger logger, Beam beam, int beamIndex)
        {
            string prefix = "Beam[" + beamIndex.ToString(CultureInfo.InvariantCulture) + "].";
            List<ControlPoint> controlPoints = beam.ControlPoints.ToList();

            logger.Log(prefix + "Id", beam.Id);
            logger.Log(prefix + "IsocentreDicomMm", FormatVector(beam.IsocenterPosition));
            logger.Log(prefix + "ControlPointCount", controlPoints.Count);
            logger.Log(prefix + "MlcModel", beam.MLC == null ? "<none>" : beam.MLC.Model);
            logger.Log(prefix + "MlcPlanType", beam.MLCPlanType);
            logger.Log(prefix + "EnergyMode", beam.EnergyModeDisplayName);
            logger.Log(prefix + "ArcLengthDeg", FormatDouble(beam.ArcLength));

            if (controlPoints.Count == 0)
            {
                logger.Log(prefix + "SourceLocationDicomMm", "<unavailable: no control points>");
                logger.Log(prefix + "LeafArrayDimensions", "<unavailable: no control points>");
                return;
            }

            ControlPoint firstControlPoint = controlPoints[0];
            VVector sourceLocation = beam.GetSourceLocation(firstControlPoint.GantryAngle);
            logger.Log(prefix + "SourceLocationDicomMm", FormatVector(sourceLocation));
            logger.Log(
                prefix + "SourceToIsocentreDistanceMm",
                FormatDouble(VVector.Distance(sourceLocation, beam.IsocenterPosition)));

            for (int controlPointIndex = 0;
                controlPointIndex < controlPoints.Count;
                controlPointIndex++)
            {
                ControlPoint controlPoint = controlPoints[controlPointIndex];
                string controlPointPrefix = prefix
                    + "ControlPoint["
                    + controlPointIndex.ToString(CultureInfo.InvariantCulture)
                    + "].";

                logger.Log(
                    controlPointPrefix + "GantryAngleDeg",
                    FormatDouble(controlPoint.GantryAngle));
                logger.Log(
                    controlPointPrefix + "CollimatorAngleDeg",
                    FormatDouble(controlPoint.CollimatorAngle));
                logger.Log(
                    controlPointPrefix + "PatientSupportAngleDeg",
                    FormatDouble(controlPoint.PatientSupportAngle));
                logger.Log(
                    controlPointPrefix + "JawPositionsBldMm",
                    FormatRectangle(controlPoint.JawPositions));

                float[,] leafPositions = controlPoint.LeafPositions;
                logger.Log(
                    controlPointPrefix + "LeafArrayDimensions",
                    FormatDimensions(
                        leafPositions.GetLength(0),
                        leafPositions.GetLength(1)));
            }
        }

        private static void LogCoreGeometry(
            Logger logger,
            StaticBeamAperture aperture,
            int beamIndex)
        {
            string prefix = "Beam[" + beamIndex.ToString(CultureInfo.InvariantCulture) + "].Core.";
            BeamCoordinateSystem coordinates = aperture.Projection.CoordinateSystem;

            logger.Log(prefix + "StaticGeometryValidation", "Supported");
            logger.Log(prefix + "SelectedControlPointIndex", 0);
            logger.Log(prefix + "ReferenceSuperiorDicom", "(0, 0, 1)");
            logger.Log(
                prefix + "AngleToleranceDeg",
                FormatDouble(EsapiBeamGeometryFactory.AngleToleranceDegrees));
            logger.Log(
                prefix + "PositionToleranceMm",
                FormatDouble(EsapiBeamGeometryFactory.PositionToleranceMm));
            logger.Log(prefix + "WAxisSourceToIsocentre", FormatVector(coordinates.WAxis));
            logger.Log(prefix + "UAxisBldX", FormatVector(coordinates.UAxis));
            logger.Log(prefix + "VAxisBldY", FormatVector(coordinates.VAxis));
            logger.Log(prefix + "ValidatedJawBoundsBldMm", FormatRectangle(aperture.Jaws.Bounds));

            LogProjectionDebug(logger, aperture, prefix, "Isocentre", coordinates.Isocentre);
            LogProjectionDebug(
                logger,
                aperture,
                prefix,
                "DicomPlus10X",
                coordinates.Isocentre + new VVector(10.0, 0.0, 0.0));
            LogProjectionDebug(
                logger,
                aperture,
                prefix,
                "DicomPlus10Y",
                coordinates.Isocentre + new VVector(0.0, 10.0, 0.0));
            LogProjectionDebug(
                logger,
                aperture,
                prefix,
                "DicomPlus10Z",
                coordinates.Isocentre + new VVector(0.0, 0.0, 10.0));
        }

        private static void LogProjectionDebug(
            Logger logger,
            StaticBeamAperture aperture,
            string beamPrefix,
            string pointName,
            VVector patientPoint)
        {
            ProjectedBeamPoint projected = aperture.Projection.Project(patientPoint);
            string prefix = beamPrefix + "DebugPoint[" + pointName + "].";

            logger.Log(prefix + "DicomPointMm", FormatVector(patientPoint));
            logger.Log(prefix + "ProjectionParameter", FormatDouble(projected.ProjectionParameter));
            logger.Log(prefix + "ProjectedDicomPointMm", FormatVector(projected.ProjectedPoint));
            logger.Log(prefix + "XBldMm", FormatDouble(projected.XBld));
            logger.Log(prefix + "YBldMm", FormatDouble(projected.YBld));
            logger.Log(prefix + "InsideJaws", aperture.Jaws.Contains(projected.XBld, projected.YBld));
        }

        private void ReportFailure(
            Logger logger,
            Stopwatch stopwatch,
            string patientId,
            string message)
        {
            stopwatch.Stop();
            logger.Log("Status", "Input rejected");
            logger.Log("FailureReason", message);
            logger.LogTiming("Phase5BeamGeometryInspection", stopwatch.ElapsedMilliseconds);

            string logError = TryWriteLog(logger, patientId);
            string logMessage = logError == null
                ? "\n\nA debug log was written to:\n" + _logDirectory
                : "\n\nThe debug log could not be written:\n" + logError;

            MessageBox.Show(
                message + logMessage,
                ApplicationName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private static string TryWriteLog(Logger logger, string patientId)
        {
            try
            {
                logger.WriteToFile(patientId);
                return null;
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
        }

        private static string FormatVector(VVector vector)
        {
            return "(" + FormatDouble(vector.x)
                + ", " + FormatDouble(vector.y)
                + ", " + FormatDouble(vector.z) + ")";
        }

        private static string FormatRectangle(VRect<double> rectangle)
        {
            return "(X1=" + FormatDouble(rectangle.X1)
                + ", Y1=" + FormatDouble(rectangle.Y1)
                + ", X2=" + FormatDouble(rectangle.X2)
                + ", Y2=" + FormatDouble(rectangle.Y2) + ")";
        }

        private static string FormatDimensions(int first, int second)
        {
            return first.ToString(CultureInfo.InvariantCulture)
                + " x "
                + second.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatDimensions(int first, int second, int third)
        {
            return first.ToString(CultureInfo.InvariantCulture)
                + " x "
                + second.ToString(CultureInfo.InvariantCulture)
                + " x "
                + third.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatDimensions(double first, double second, double third)
        {
            return FormatDouble(first)
                + " x "
                + FormatDouble(second)
                + " x "
                + FormatDouble(third);
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("G17", CultureInfo.InvariantCulture);
        }
    }
}
