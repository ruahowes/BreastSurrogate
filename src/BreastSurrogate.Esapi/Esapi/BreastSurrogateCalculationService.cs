using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BreastSurrogate.Core.Apertures;
using Uclh.XRT.Esapi.Core;
using Uclh.XRT.Esapi.Utilities;
using VMS.TPS.Common.Model.API;

namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// Presentation-free, read-only geometric surrogate calculation.
    /// </summary>
    public sealed class BreastSurrogateCalculationService
    {
        public const string RequiredField1BeamId = "ANT MED";
        public const string RequiredField2BeamId = "POST LAT";
        public BreastSurrogateCalculationResult Calculate(EsapiContext context)
        {
            string patientId = context != null && context.pa != null
                ? context.pa.Id
                : null;
            string planId = context != null && context.ps != null
                ? context.ps.Id
                : null;
            string validationError = ValidateContext(context);
            if (validationError != null)
            {
                return CreateSharedFailure(patientId, planId, 0, validationError);
            }

            List<Beam> treatmentBeams = BeamAnalyzer
                .GetTreatmentBeams(context.ps)
                .ToList();
            List<Beam> field1Matches = FindBeamMatches(
                treatmentBeams,
                RequiredField1BeamId);
            List<Beam> field2Matches = FindBeamMatches(
                treatmentBeams,
                RequiredField2BeamId);

            if (field1Matches.Count != 1 || field2Matches.Count != 1)
            {
                string reason = "BreastSurrogate requires exactly one treatment beam with ID '"
                    + RequiredField1BeamId
                    + "' and exactly one with ID '"
                    + RequiredField2BeamId
                    + "'. Found "
                    + field1Matches.Count.ToString(CultureInfo.InvariantCulture)
                    + " and "
                    + field2Matches.Count.ToString(CultureInfo.InvariantCulture)
                    + ", respectively.";
                return CreateSharedFailure(
                    patientId,
                    planId,
                    treatmentBeams.Count,
                    reason);
            }

            Beam field1Beam = field1Matches[0];
            Beam field2Beam = field2Matches[0];
            SelectedBeamCalculation field1;
            SelectedBeamCalculation field2;

            try
            {
                var factory = new EsapiBeamGeometryFactory();
                field1 = CreateSelectedBeam(
                    factory,
                    field1Beam,
                    treatmentBeams,
                    context.im);
                field2 = CreateSelectedBeam(
                    factory,
                    field2Beam,
                    treatmentBeams,
                    context.im);
            }
            catch (UnsupportedBeamGeometryException exception)
            {
                return CreateSharedFailure(
                    patientId,
                    planId,
                    treatmentBeams.Count,
                    exception.Message);
            }

            var sampler = new StructureVoxelSampler();
            IpsilateralLungSelectionDiagnostics lungDiagnostics = null;
            SurrogateMetricResult geometricIlf;
            try
            {
                var lungSelector = new IpsilateralLungSelector();
                IpsilateralLungSelectionResult lungSelection = lungSelector.Select(
                    context.ss.Structures,
                    field1Beam.IsocenterPosition);
                lungDiagnostics = CreateLungDiagnostics(lungSelection);
                geometricIlf = SampleMetric(
                    "gILF",
                    lungSelection.SelectedStructure,
                    context.im,
                    field1.Aperture,
                    field2.Aperture,
                    sampler);
            }
            catch (IpsilateralLungSelectionException exception)
            {
                geometricIlf = SurrogateMetricResult.Unavailable(
                    "gILF",
                    null,
                    LegacyStructureMetricService.MapLungFailure(exception),
                    exception.Message);
            }

            var heartSelector = new HeartStructureSelector();
            EsapiStructureSelectionResult heartSelection = heartSelector.Select(
                context.ss.Structures);
            SurrogateMetricResult geometricHif = heartSelection.IsSelected
                ? SampleMetric(
                    "gHIF",
                    heartSelection.SelectedStructure,
                    context.im,
                    field1.Aperture,
                    field2.Aperture,
                    sampler)
                : SurrogateMetricResult.Unavailable(
                    "gHIF",
                    null,
                    LegacyStructureMetricService.MapIdSelectionFailure(
                        heartSelection.Diagnostics),
                    heartSelection.Diagnostics.FailureReason);

            return new BreastSurrogateCalculationResult(
                patientId,
                planId,
                treatmentBeams.Count,
                field1,
                field2,
                lungDiagnostics,
                heartSelection.Diagnostics,
                geometricIlf,
                geometricHif,
                null);
        }

        private static SurrogateMetricResult SampleMetric(
            string metricName,
            Structure structure,
            Image image,
            StaticBeamAperture field1,
            StaticBeamAperture field2,
            StructureVoxelSampler sampler)
        {
            try
            {
                StructureVoxelSamplingResult samplingResult = sampler.Sample(
                    structure,
                    image,
                    field1,
                    field2);
                return SurrogateMetricResult.Available(
                    metricName,
                    samplingResult);
            }
            catch (StructureVoxelSamplingException exception)
            {
                return SurrogateMetricResult.Unavailable(
                    metricName,
                    structure.Id,
                    MetricCalculationStatus.CalculationFailed,
                    exception.Message);
            }
        }

        private static SelectedBeamCalculation CreateSelectedBeam(
            EsapiBeamGeometryFactory factory,
            Beam beam,
            IList<Beam> treatmentBeams,
            Image image)
        {
            StaticBeamAperture aperture = factory.Create(
                beam,
                image.ImagingOrientation);
            return new SelectedBeamCalculation(
                beam.Id,
                treatmentBeams.IndexOf(beam),
                beam.ControlPoints.Count(),
                aperture);
        }

        private static List<Beam> FindBeamMatches(
            IEnumerable<Beam> treatmentBeams,
            string requiredId)
        {
            return treatmentBeams.Where(
                beam => string.Equals(
                    beam.Id,
                    requiredId,
                    StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private static IpsilateralLungSelectionDiagnostics CreateLungDiagnostics(
            IpsilateralLungSelectionResult result)
        {
            var candidates = result.Candidates.Select(
                candidate => new IpsilateralLungCandidateDiagnostics(
                    candidate.Structure.Id,
                    candidate.Structure.DicomType,
                    candidate.CenterPoint,
                    candidate.DistanceToIsocentreMm)).ToList();

            return new IpsilateralLungSelectionDiagnostics(
                result.SelectionMethod,
                result.SelectedStructure.Id,
                result.ReferenceIsocentre,
                candidates);
        }

        private static BreastSurrogateCalculationResult CreateSharedFailure(
            string patientId,
            string planId,
            int treatmentBeamCount,
            string reason)
        {
            return new BreastSurrogateCalculationResult(
                patientId,
                planId,
                treatmentBeamCount,
                null,
                null,
                null,
                null,
                SurrogateMetricResult.Unavailable(
                    "gILF", null, MetricCalculationStatus.Unsupported, reason),
                SurrogateMetricResult.Unavailable(
                    "gHIF", null, MetricCalculationStatus.Unsupported, reason),
                reason);
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
    }
}
