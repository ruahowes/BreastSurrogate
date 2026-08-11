using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BreastSurrogate.Core.Apertures;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Esapi
{
    public enum BreastSurrogateCalculationStatus
    {
        Success,
        PartialSuccess,
        Unavailable
    }

    /// <summary>
    /// Structured result returned by the presentation-free ESAPI calculation.
    /// Contains identifiers, copied values and Core objects, but no persistent
    /// ESAPI API object.
    /// </summary>
    public sealed class BreastSurrogateCalculationResult
    {
        public BreastSurrogateCalculationResult(
            string patientId,
            string planId,
            int treatmentBeamCount,
            SelectedBeamCalculation field1,
            SelectedBeamCalculation field2,
            IpsilateralLungSelectionDiagnostics lungSelection,
            StructureIdSelectionResult heartSelection,
            SurrogateMetricResult geometricIlf,
            SurrogateMetricResult geometricHif,
            string sharedFailureReason)
        {
            if (geometricIlf == null)
            {
                throw new ArgumentNullException("geometricIlf");
            }

            if (geometricHif == null)
            {
                throw new ArgumentNullException("geometricHif");
            }

            if (treatmentBeamCount < 0)
            {
                throw new ArgumentOutOfRangeException("treatmentBeamCount");
            }

            PatientId = patientId;
            PlanId = planId;
            TreatmentBeamCount = treatmentBeamCount;
            Field1 = field1;
            Field2 = field2;
            LungSelection = lungSelection;
            HeartSelection = heartSelection;
            GeometricIlf = geometricIlf;
            GeometricHif = geometricHif;
            SharedFailureReason = sharedFailureReason;
        }

        public string PatientId { get; private set; }

        public string PlanId { get; private set; }

        public int TreatmentBeamCount { get; private set; }

        public SelectedBeamCalculation Field1 { get; private set; }

        public SelectedBeamCalculation Field2 { get; private set; }

        public int IgnoredTreatmentBeamCount
        {
            get { return Field1 == null || Field2 == null ? 0 : TreatmentBeamCount - 2; }
        }

        public IpsilateralLungSelectionDiagnostics LungSelection { get; private set; }

        public StructureIdSelectionResult HeartSelection { get; private set; }

        public SurrogateMetricResult GeometricIlf { get; private set; }

        public SurrogateMetricResult GeometricHif { get; private set; }

        public string SharedFailureReason { get; private set; }

        public BreastSurrogateCalculationStatus Status
        {
            get
            {
                if (GeometricIlf.IsAvailable && GeometricHif.IsAvailable)
                {
                    return BreastSurrogateCalculationStatus.Success;
                }

                if (GeometricIlf.IsAvailable || GeometricHif.IsAvailable)
                {
                    return BreastSurrogateCalculationStatus.PartialSuccess;
                }

                return BreastSurrogateCalculationStatus.Unavailable;
            }
        }
    }

    public sealed class SelectedBeamCalculation
    {
        public SelectedBeamCalculation(
            string beamId,
            int treatmentBeamIndex,
            int controlPointCount,
            StaticBeamAperture aperture)
        {
            if (string.IsNullOrWhiteSpace(beamId))
            {
                throw new ArgumentException("Beam ID cannot be null or empty.", "beamId");
            }

            if (treatmentBeamIndex < 0)
            {
                throw new ArgumentOutOfRangeException("treatmentBeamIndex");
            }

            if (controlPointCount < 0)
            {
                throw new ArgumentOutOfRangeException("controlPointCount");
            }

            if (aperture == null)
            {
                throw new ArgumentNullException("aperture");
            }

            BeamId = beamId;
            TreatmentBeamIndex = treatmentBeamIndex;
            ControlPointCount = controlPointCount;
            Aperture = aperture;
        }

        public string BeamId { get; private set; }

        public int TreatmentBeamIndex { get; private set; }

        public int ControlPointCount { get; private set; }

        public StaticBeamAperture Aperture { get; private set; }
    }

    public sealed class IpsilateralLungSelectionDiagnostics
    {
        private readonly ReadOnlyCollection<IpsilateralLungCandidateDiagnostics> _candidates;

        public IpsilateralLungSelectionDiagnostics(
            string selectionMethod,
            string selectedStructureId,
            VVector referenceIsocentre,
            IList<IpsilateralLungCandidateDiagnostics> candidates)
        {
            if (string.IsNullOrWhiteSpace(selectionMethod))
            {
                throw new ArgumentException(
                    "Selection method cannot be null or empty.",
                    "selectionMethod");
            }

            if (string.IsNullOrWhiteSpace(selectedStructureId))
            {
                throw new ArgumentException(
                    "Selected structure ID cannot be null or empty.",
                    "selectedStructureId");
            }

            if (candidates == null)
            {
                throw new ArgumentNullException("candidates");
            }

            SelectionMethod = selectionMethod;
            SelectedStructureId = selectedStructureId;
            ReferenceIsocentre = referenceIsocentre;
            _candidates = new ReadOnlyCollection<IpsilateralLungCandidateDiagnostics>(
                new List<IpsilateralLungCandidateDiagnostics>(candidates));
        }

        public string SelectionMethod { get; private set; }

        public string SelectedStructureId { get; private set; }

        public VVector ReferenceIsocentre { get; private set; }

        public IList<IpsilateralLungCandidateDiagnostics> Candidates
        {
            get { return _candidates; }
        }
    }

    public sealed class IpsilateralLungCandidateDiagnostics
    {
        public IpsilateralLungCandidateDiagnostics(
            string structureId,
            string dicomType,
            VVector centerPoint,
            double distanceToIsocentreMm)
        {
            StructureId = structureId;
            DicomType = dicomType;
            CenterPoint = centerPoint;
            DistanceToIsocentreMm = distanceToIsocentreMm;
        }

        public string StructureId { get; private set; }

        public string DicomType { get; private set; }

        public VVector CenterPoint { get; private set; }

        public double DistanceToIsocentreMm { get; private set; }
    }
}
