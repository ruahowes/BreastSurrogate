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
    /// Read-only calculation runner hosted through the shared-library ESAPI context.
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

        public void Run(EsapiContext context)
        {
            var logger = new Logger(_logDirectory, ApplicationName, true);
            var stopwatch = Stopwatch.StartNew();
            string patientId = UnknownPatientId;

            try
            {
                logger.LogMethodStart();

                var service = new BreastSurrogateCalculationService();
                BreastSurrogateCalculationResult result = service.Calculate(context);
                if (!string.IsNullOrWhiteSpace(result.PatientId))
                {
                    patientId = result.PatientId;
                }

                if (ValidateContext(context) == null)
                {
                    LogContext(logger, context);
                    List<Beam> treatmentBeams = BeamAnalyzer
                        .GetTreatmentBeams(context.ps)
                        .ToList();
                    logger.Log("TreatmentBeamCount", treatmentBeams.Count);

                    for (int beamIndex = 0; beamIndex < treatmentBeams.Count; beamIndex++)
                    {
                        LogBeam(logger, treatmentBeams[beamIndex], beamIndex);
                    }
                }

                LogCalculationResult(logger, context, result);
                LogResultsSummary(logger, result);

                if (!string.IsNullOrWhiteSpace(result.SharedFailureReason))
                {
                    ReportFailure(
                        logger,
                        stopwatch,
                        patientId,
                        result.SharedFailureReason);
                    return;
                }

                stopwatch.Stop();
                logger.Log("Phase9.TotalElapsedMs", stopwatch.ElapsedMilliseconds);
                logger.Log("Status", FormatCalculationStatus(result.Status));

                string logError = TryWriteLog(logger, patientId);
                if (logError != null)
                {
                    MessageBox.Show(
                        "BreastSurrogate inspected " + result.TreatmentBeamCount
                        + " treatment beam(s), but the debug log could not be written.\n\n"
                        + logError,
                        ApplicationName,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                MessageBox.Show(
                    FormatPhase9Summary(result)
                        + "\nDebug log directory:\n"
                        + _logDirectory,
                    ApplicationName,
                    MessageBoxButton.OK,
                    result.Status == BreastSurrogateCalculationStatus.Success
                        ? MessageBoxImage.Information
                        : MessageBoxImage.Warning);
            }
            catch (UnsupportedBeamGeometryException exception)
            {
                ReportFailure(logger, stopwatch, patientId, exception.Message);
            }
            catch (IpsilateralLungSelectionException exception)
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
                logger.Log("Phase9.TotalElapsedMs", stopwatch.ElapsedMilliseconds);

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

        private static string ValidateContext(EsapiContext context)
        {
            if (context == null)
            {
                return "No ESAPI context was provided.";
            }

            if (context.pa == null)
            {
                return "The ESAPI context does not contain a patient.";
            }

            if (context.ps == null)
            {
                return "The ESAPI context does not contain a treatment plan.";
            }

            if (context.ss == null)
            {
                return "The open plan has no associated structure set.";
            }

            if (context.im == null)
            {
                return "The open plan has no associated 3D image.";
            }

            return null;
        }

        private static void LogCalculationResult(
            Logger logger,
            EsapiContext context,
            BreastSurrogateCalculationResult result)
        {
            if (result.Field1 != null && result.Field2 != null)
            {
                LogCoreGeometry(
                    logger,
                    result.Field1.Aperture,
                    result.Field1.TreatmentBeamIndex,
                    result.Field1.ControlPointCount);
                LogCoreGeometry(
                    logger,
                    result.Field2.Aperture,
                    result.Field2.TreatmentBeamIndex,
                    result.Field2.ControlPointCount);
                logger.Log("Phase9.Field1BeamId", result.Field1.BeamId);
                logger.Log("Phase9.Field2BeamId", result.Field2.BeamId);
                logger.Log(
                    "Phase9.IgnoredTreatmentBeamCount",
                    result.IgnoredTreatmentBeamCount);
            }

            if (result.LungSelection != null)
            {
                LogIpsilateralLungSelection(logger, result.LungSelection);
            }

            logger.Log(
                "Phase9.IpsilateralLungStatus",
                FormatStructureStatus(result.GeometricIlf));
            logger.Log(
                "Phase9.HeartStatus",
                FormatStructureStatus(result.GeometricHif));

            if (context == null || context.ss == null || context.im == null)
            {
                return;
            }

            LogAvailableStructureSampling(
                logger,
                context,
                result.GeometricIlf,
                0);
            LogAvailableStructureSampling(
                logger,
                context,
                result.GeometricHif,
                1);
        }

        private static void LogAvailableStructureSampling(
            Logger logger,
            EsapiContext context,
            SurrogateMetricResult metric,
            int structureIndex)
        {
            if (!metric.IsAvailable || metric.SamplingResult == null)
            {
                return;
            }

            Structure structure = context.ss.Structures.FirstOrDefault(
                candidate => string.Equals(
                    candidate.Id,
                    metric.StructureId,
                    StringComparison.OrdinalIgnoreCase));
            if (structure == null)
            {
                logger.Log(
                    "Phase9.StructureCalculation["
                    + structureIndex.ToString(CultureInfo.InvariantCulture)
                    + "].LoggingWarning",
                    "The selected structure could not be resolved for presentation logging.");
                return;
            }

            LogStructureSampling(
                logger,
                structure,
                context.im,
                metric.SamplingResult,
                structureIndex);
        }

        private static void LogResultsSummary(
            Logger logger,
            BreastSurrogateCalculationResult result)
        {
            logger.Log("Results.OverallStatus", FormatCalculationStatus(result.Status));
            LogMetricResult(logger, "Results.gILF.", result.GeometricIlf);
            LogMetricResult(logger, "Results.gHIF.", result.GeometricHif);
        }

        private static void LogMetricResult(
            Logger logger,
            string prefix,
            SurrogateMetricResult metric)
        {
            logger.Log(prefix + "Status", metric.Status);
            logger.Log(
                prefix + "StructureId",
                string.IsNullOrWhiteSpace(metric.StructureId)
                    ? "<unavailable>"
                    : metric.StructureId);

            if (metric.IsAvailable)
            {
                logger.Log(prefix + "ValuePercent", FormatDouble(metric.Value.Value));
            }
            else
            {
                logger.Log(prefix + "ValuePercent", "<unavailable>");
                logger.Log(prefix + "FailureReason", metric.FailureReason);
            }
        }

        private static string FormatStructureStatus(SurrogateMetricResult metric)
        {
            return metric.IsAvailable
                ? "Selected '" + metric.StructureId + "'"
                : "Unavailable: " + metric.FailureReason;
        }

        private static string FormatCalculationStatus(
            BreastSurrogateCalculationStatus status)
        {
            switch (status)
            {
                case BreastSurrogateCalculationStatus.Success:
                    return "Success";
                case BreastSurrogateCalculationStatus.PartialSuccess:
                    return "Partial success";
                default:
                    return "Unavailable";
            }
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
            int beamIndex,
            int controlPointCount)
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

            MlcAperture mlc = aperture.Mlc;
            logger.Log(prefix + "ConfiguredMlcModel", mlc.GeometryDefinition.ModelIdentifier);
            logger.Log(
                prefix + "ValidatedLeafArrayDimensions",
                FormatDimensions(2, mlc.GeometryDefinition.LeafPairCount));
            logger.Log(
                prefix + "MlcLeafSpanYBldMm",
                FormatDouble(mlc.GeometryDefinition.MinimumYBldMm)
                + ".."
                + FormatDouble(mlc.GeometryDefinition.MaximumYBldMm));
            logger.Log(
                prefix + "MlcBankConvention",
                "Bank 0 = negative BLD X; bank 1 = positive BLD X (ESAPI documented)");
            logger.Log(
                prefix + "MlcLeafIndexDirection",
                "Leaf index 0 = negative BLD Y; validated against Eclipse representative leaves");
            logger.Log(
                prefix + "MlcStaticControlPointValidation",
                "Validated unchanged leaf positions across "
                + controlPointCount.ToString(CultureInfo.InvariantCulture)
                + " control points");
            LogRepresentativeMlcLeaves(logger, mlc, prefix);

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

            int leafPairIndex;
            if (!aperture.Mlc.GeometryDefinition.TryGetLeafPairIndex(
                projected.YBld,
                out leafPairIndex))
            {
                logger.Log(prefix + "SelectedLeafPairIndexZeroBased", "<outside MLC leaf span>");
                logger.Log(prefix + "InsideMlc", false);
            }
            else
            {
                logger.Log(prefix + "SelectedLeafPairIndexZeroBased", leafPairIndex);
                logger.Log(prefix + "SelectedVarianLeafPairNumber", leafPairIndex + 1);
                logger.Log(
                    prefix + "SelectedLeafYBoundsBldMm",
                    FormatDouble(aperture.Mlc.GeometryDefinition.GetLeafLowerBoundaryMm(leafPairIndex))
                    + ".."
                    + FormatDouble(aperture.Mlc.GeometryDefinition.GetLeafUpperBoundaryMm(leafPairIndex)));
                logger.Log(
                    prefix + "NegativeXBankPositionMm",
                    FormatDouble(aperture.Mlc.GetNegativeXBankPositionMm(leafPairIndex)));
                logger.Log(
                    prefix + "PositiveXBankPositionMm",
                    FormatDouble(aperture.Mlc.GetPositiveXBankPositionMm(leafPairIndex)));
                logger.Log(
                    prefix + "InsideMlc",
                    aperture.Mlc.Contains(projected.XBld, projected.YBld));
            }

            logger.Log(prefix + "InsideFinalAperture", aperture.Contains(patientPoint));
        }

        private static void LogRepresentativeMlcLeaves(
            Logger logger,
            MlcAperture mlc,
            string beamPrefix)
        {
            int[] representativeLeafPairIndices = { 0, 9, 10, 29, 30, 49, 50, 59 };

            foreach (int leafPairIndex in representativeLeafPairIndices)
            {
                string prefix = beamPrefix
                    + "MlcLeaf["
                    + leafPairIndex.ToString(CultureInfo.InvariantCulture)
                    + "].";
                logger.Log(prefix + "VarianLeafPairNumber", leafPairIndex + 1);
                logger.Log(
                    prefix + "YBoundsBldMm",
                    FormatDouble(mlc.GeometryDefinition.GetLeafLowerBoundaryMm(leafPairIndex))
                    + ".."
                    + FormatDouble(mlc.GeometryDefinition.GetLeafUpperBoundaryMm(leafPairIndex)));
                logger.Log(
                    prefix + "NegativeXBankPositionMm",
                    FormatDouble(mlc.GetNegativeXBankPositionMm(leafPairIndex)));
                logger.Log(
                    prefix + "PositiveXBankPositionMm",
                    FormatDouble(mlc.GetPositiveXBankPositionMm(leafPairIndex)));
            }
        }

        private static void LogStructureSampling(
            Logger logger,
            Structure structure,
            Image image,
            StructureVoxelSamplingResult result,
            int structureIndex)
        {
            string prefix = "Phase9.StructureCalculation["
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

        private static void LogIpsilateralLungSelection(
            Logger logger,
            IpsilateralLungSelectionDiagnostics result)
        {
            const string prefix = "Phase9.IpsilateralLungSelection.";
            logger.Log(prefix + "Method", result.SelectionMethod);
            logger.Log(prefix + "SelectedStructureId", result.SelectedStructureId);
            logger.Log(prefix + "ReferenceIsocentreDicomMm", FormatVector(result.ReferenceIsocentre));
            logger.Log(prefix + "CandidateCount", result.Candidates.Count);

            for (int index = 0; index < result.Candidates.Count; index++)
            {
                IpsilateralLungCandidateDiagnostics candidate = result.Candidates[index];
                string candidatePrefix = prefix
                    + "Candidate["
                    + index.ToString(CultureInfo.InvariantCulture)
                    + "].";
                logger.Log(candidatePrefix + "StructureId", candidate.StructureId);
                logger.Log(candidatePrefix + "DicomType", candidate.DicomType);
                logger.Log(candidatePrefix + "CenterPointDicomMm", FormatVector(candidate.CenterPoint));
                logger.Log(
                    candidatePrefix + "DistanceToIsocentreMm",
                    FormatDouble(candidate.DistanceToIsocentreMm));
            }
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

        private static string FormatPhase9Summary(
            BreastSurrogateCalculationResult result)
        {
            var summary = new StringBuilder();
            summary.Append("BreastSurrogate Phase 9 jaw + MLC results\n\n");

            AppendMetricSummary(summary, result.GeometricIlf);
            AppendMetricSummary(summary, result.GeometricHif);

            summary.Append("\nDevelopment result: jaws and Millennium 120 MLC included.\n");

            return summary.ToString();
        }

        private static void AppendMetricSummary(
            StringBuilder summary,
            SurrogateMetricResult metric)
        {
            if (!metric.IsAvailable || metric.SamplingResult == null)
            {
                summary.Append(metric.MetricName);
                summary.Append(": unavailable — ");
                summary.Append(metric.FailureReason);
                summary.Append("\n");
                return;
            }

            StructureVoxelSamplingResult sampling = metric.SamplingResult;
            InFieldCalculationResult inField = sampling.InFieldResult;
            summary.Append(sampling.StructureId);
            summary.Append(" ");
            summary.Append(metric.MetricName);
            summary.Append(": ");
            summary.Append(metric.Value.Value.ToString("F3", CultureInfo.InvariantCulture));
            summary.Append(" %\n  Field 1: ");
            summary.Append(inField.Field1PercentageOfEsapiVolume.ToString("F3", CultureInfo.InvariantCulture));
            summary.Append(" %, Field 2: ");
            summary.Append(inField.Field2PercentageOfEsapiVolume.ToString("F3", CultureInfo.InvariantCulture));
            summary.Append(" %, Both: ");
            summary.Append(inField.BothFieldsPercentageOfEsapiVolume.ToString("F3", CultureInfo.InvariantCulture));
            summary.Append(" %; ");
            summary.Append(sampling.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            summary.Append(" ms\n");
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
            logger.Log("Phase9.TotalElapsedMs", stopwatch.ElapsedMilliseconds);

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
