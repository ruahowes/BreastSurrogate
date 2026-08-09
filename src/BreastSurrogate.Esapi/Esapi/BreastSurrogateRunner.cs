using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using BreastSurrogate.Core.Apertures;
using BreastSurrogate.Core.Calculation;
using BreastSurrogate.Core.Geometry;
using Uclh.XRT.Esapi.Core;
using Uclh.XRT.Esapi.Utilities;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// Read-only development runner for inspecting beam geometry and structure sampling.
    /// </summary>
    public sealed class BreastSurrogateRunner
    {
        private const string ApplicationName = "BreastSurrogate";
        private const string RequiredField1BeamId = "ANT MED";
        private const string RequiredField2BeamId = "POST LAT";
        private const string RequiredLungId = "IPS LUNG";
        private const string OptionalHeartId = "Heart";
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

                for (int beamIndex = 0; beamIndex < treatmentBeams.Count; beamIndex++)
                {
                    LogBeam(logger, treatmentBeams[beamIndex], beamIndex);
                }

                List<Beam> field1Matches = treatmentBeams.Where(
                    beam => string.Equals(
                        beam.Id,
                        RequiredField1BeamId,
                        StringComparison.OrdinalIgnoreCase)).ToList();
                List<Beam> field2Matches = treatmentBeams.Where(
                    beam => string.Equals(
                        beam.Id,
                        RequiredField2BeamId,
                        StringComparison.OrdinalIgnoreCase)).ToList();

                if (field1Matches.Count != 1 || field2Matches.Count != 1)
                {
                    ReportFailure(
                        logger,
                        stopwatch,
                        patientId,
                        "Phase 7 requires exactly one treatment beam with ID '"
                        + RequiredField1BeamId
                        + "' and exactly one with ID '"
                        + RequiredField2BeamId
                        + "'. Found "
                        + field1Matches.Count.ToString(CultureInfo.InvariantCulture)
                        + " and "
                        + field2Matches.Count.ToString(CultureInfo.InvariantCulture)
                        + ", respectively.");
                    return;
                }

                var selectedBeams = new List<Beam> { field1Matches[0], field2Matches[0] };
                var factory = new EsapiBeamGeometryFactory();
                var apertures = new List<StaticBeamAperture>();
                for (int fieldIndex = 0; fieldIndex < selectedBeams.Count; fieldIndex++)
                {
                    Beam beam = selectedBeams[fieldIndex];

                    StaticBeamAperture aperture = factory.Create(
                        beam,
                        context.im.ImagingOrientation);
                    apertures.Add(aperture);
                    LogCoreGeometry(logger, aperture, treatmentBeams.IndexOf(beam));
                }

                logger.Log("Phase7.Field1BeamId", selectedBeams[0].Id);
                logger.Log("Phase7.Field2BeamId", selectedBeams[1].Id);
                logger.Log(
                    "Phase7.IgnoredTreatmentBeamCount",
                    treatmentBeams.Count - selectedBeams.Count);

                Structure ipsilateralLung = context.ss.Structures.FirstOrDefault(
                    structure => string.Equals(
                        structure.Id,
                        RequiredLungId,
                        StringComparison.OrdinalIgnoreCase));
                if (ipsilateralLung == null)
                {
                    ReportFailure(
                        logger,
                        stopwatch,
                        patientId,
                        "Phase 7 requires a structure with ID '" + RequiredLungId + "'.");
                    return;
                }

                Structure heart = context.ss.Structures.FirstOrDefault(
                    structure => string.Equals(
                        structure.Id,
                        OptionalHeartId,
                        StringComparison.OrdinalIgnoreCase));
                logger.Log("Phase7.IpsilateralLungStatus", "Present");
                logger.Log(
                    "Phase7.HeartStatus",
                    heart == null ? "Not present (optional)" : "Present");

                var targetStructures = new List<Structure> { ipsilateralLung };
                if (heart != null)
                {
                    targetStructures.Add(heart);
                }

                var sampler = new StructureVoxelSampler();
                var samplingResults = new List<StructureVoxelSamplingResult>();
                for (int structureIndex = 0;
                    structureIndex < targetStructures.Count;
                    structureIndex++)
                {
                    Structure structure = targetStructures[structureIndex];
                    StructureVoxelSamplingResult samplingResult = sampler.Sample(
                        structure,
                        context.im,
                        apertures[0],
                        apertures[1]);
                    samplingResults.Add(samplingResult);
                    LogStructureSampling(
                        logger,
                        structure,
                        context.im,
                        samplingResult,
                        structureIndex);
                }

                logger.Log(
                    "Phase7.gILFPercent",
                    FormatDouble(samplingResults[0].InFieldResult.EitherFieldPercentageOfEsapiVolume));
                if (heart != null)
                {
                    logger.Log(
                        "Phase7.gHIFPercent",
                        FormatDouble(samplingResults[1].InFieldResult.EitherFieldPercentageOfEsapiVolume));
                }
                else
                {
                    logger.Log("Phase7.gHIFPercent", "<unavailable: Heart not present>");
                }

                stopwatch.Stop();
                logger.Log("Phase7.TotalElapsedMs", stopwatch.ElapsedMilliseconds);
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
                    FormatPhase7Summary(samplingResults, heart == null)
                        + "\nDebug log directory:\n"
                        + _logDirectory,
                    ApplicationName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (UnsupportedBeamGeometryException exception)
            {
                ReportFailure(logger, stopwatch, patientId, exception.Message);
            }
            catch (StructureVoxelSamplingException exception)
            {
                ReportFailure(logger, stopwatch, patientId, exception.Message);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                logger.Log("Status", "Unexpected failure");
                logger.Log("ExceptionType", exception.GetType().FullName);
                logger.Log("ExceptionMessage", exception.Message);
                logger.Log("Phase7.TotalElapsedMs", stopwatch.ElapsedMilliseconds);

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
            logger.Log(prefix + "WAxisIsocentreToSource", FormatVector(coordinates.WAxis));
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

        private static void LogStructureSampling(
            Logger logger,
            Structure structure,
            Image image,
            StructureVoxelSamplingResult result,
            int structureIndex)
        {
            string prefix = "Phase7.StructureCalculation["
                + structureIndex.ToString(CultureInfo.InvariantCulture)
                + "].";
            var bounds = structure.MeshGeometry.Bounds;

            logger.Log(prefix + "StructureId", result.StructureId);
            logger.Log(prefix + "ImageSizeVoxels", FormatDimensions(image.XSize, image.YSize, image.ZSize));
            logger.Log(prefix + "ImageResolutionMm", FormatDimensions(image.XRes, image.YRes, image.ZRes));
            logger.Log(
                prefix + "MeshBoundsDicomMm",
                "(X=" + FormatDouble(bounds.X)
                + ", Y=" + FormatDouble(bounds.Y)
                + ", Z=" + FormatDouble(bounds.Z)
                + ", SizeX=" + FormatDouble(bounds.SizeX)
                + ", SizeY=" + FormatDouble(bounds.SizeY)
                + ", SizeZ=" + FormatDouble(bounds.SizeZ) + ")");
            logger.Log(
                prefix + "VoxelIndexRangeInclusive",
                "(X=" + FormatIndexRange(result.MinimumXIndex, result.MaximumXIndex)
                + ", Y=" + FormatIndexRange(result.MinimumYIndex, result.MaximumYIndex)
                + ", Z=" + FormatIndexRange(result.MinimumZIndex, result.MaximumZIndex) + ")");
            logger.Log(prefix + "SamplingStride", 1);
            logger.Log(prefix + "SamplingMethod", "Full-resolution X-axis segment profiles");
            logger.Log(prefix + "CandidateVoxelCount", result.CandidateVoxelCount);
            logger.Log(
                prefix + "StructureMembershipQueryCount",
                result.StructureMembershipQueryCount);
            logger.Log(prefix + "InsideStructureVoxelCount", result.InsideStructureVoxelCount);
            logger.Log(prefix + "VoxelVolumeMm3", FormatDouble(result.VoxelVolumeCubicMillimetres));
            logger.Log(
                prefix + "SampledStructureVolumeCm3",
                FormatDouble(result.SampledStructureVolumeCubicCentimetres));
            logger.Log(
                prefix + "EsapiStructureVolumeCm3",
                FormatDouble(result.EsapiStructureVolumeCubicCentimetres));
            logger.Log(prefix + "PercentageDenominator", "ESAPI Structure.Volume");
            LogInFieldResult(logger, prefix, result.InFieldResult);
            logger.LogTiming(prefix + "Elapsed", result.ElapsedMilliseconds);
        }

        private static void LogInFieldResult(
            Logger logger,
            string prefix,
            InFieldCalculationResult result)
        {
            logger.Log(prefix + "TotalStructurePointCount", result.TotalStructurePointCount);
            logger.Log(prefix + "Field1PointCount", result.Field1PointCount);
            logger.Log(prefix + "Field1VolumeCm3", FormatDouble(result.Field1VolumeCubicCentimetres));
            logger.Log(prefix + "Field1Percent", FormatDouble(result.Field1PercentageOfEsapiVolume));
            logger.Log(prefix + "Field2PointCount", result.Field2PointCount);
            logger.Log(prefix + "Field2VolumeCm3", FormatDouble(result.Field2VolumeCubicCentimetres));
            logger.Log(prefix + "Field2Percent", FormatDouble(result.Field2PercentageOfEsapiVolume));
            logger.Log(prefix + "EitherFieldPointCount", result.EitherFieldPointCount);
            logger.Log(
                prefix + "EitherFieldVolumeCm3",
                FormatDouble(result.EitherFieldVolumeCubicCentimetres));
            logger.Log(
                prefix + "EitherFieldPercent",
                FormatDouble(result.EitherFieldPercentageOfEsapiVolume));
            logger.Log(prefix + "BothFieldsPointCount", result.BothFieldsPointCount);
            logger.Log(
                prefix + "BothFieldsVolumeCm3",
                FormatDouble(result.BothFieldsVolumeCubicCentimetres));
            logger.Log(
                prefix + "BothFieldsPercent",
                FormatDouble(result.BothFieldsPercentageOfEsapiVolume));
        }

        private static string FormatPhase7Summary(
            IList<StructureVoxelSamplingResult> results,
            bool heartWasAbsent)
        {
            var summary = new StringBuilder();
            summary.Append("BreastSurrogate Phase 7 jaw-only results\n\n");

            for (int index = 0; index < results.Count; index++)
            {
                StructureVoxelSamplingResult result = results[index];
                InFieldCalculationResult inField = result.InFieldResult;
                summary.Append(result.StructureId);
                summary.Append(index == 0 ? " gILF: " : " gHIF: ");
                summary.Append(inField.EitherFieldPercentageOfEsapiVolume.ToString("F3", CultureInfo.InvariantCulture));
                summary.Append(" %\n  Field 1: ");
                summary.Append(inField.Field1PercentageOfEsapiVolume.ToString("F3", CultureInfo.InvariantCulture));
                summary.Append(" %, Field 2: ");
                summary.Append(inField.Field2PercentageOfEsapiVolume.ToString("F3", CultureInfo.InvariantCulture));
                summary.Append(" %, Both: ");
                summary.Append(inField.BothFieldsPercentageOfEsapiVolume.ToString("F3", CultureInfo.InvariantCulture));
                summary.Append(" %; ");
                summary.Append(result.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
                summary.Append(" ms\n");
            }

            if (heartWasAbsent)
            {
                summary.Append("\nHeart was not present; lung sampling completed normally.\n");
            }

            summary.Append("\nDevelopment result: jaws only; MLC is not included.\n");

            return summary.ToString();
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
            logger.Log("Phase7.TotalElapsedMs", stopwatch.ElapsedMilliseconds);

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

        private static string FormatIndexRange(int minimum, int maximum)
        {
            return minimum.ToString(CultureInfo.InvariantCulture)
                + ".."
                + maximum.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("G17", CultureInfo.InvariantCulture);
        }
    }
}
