using System;
using System.Collections.Generic;
using System.Linq;
using Uclh.XRT.Esapi.Core;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Esapi
{
    public sealed class PhysicsPlanMetricResult
    {
        public PhysicsPlanMetricResult(
            BreastSurrogateCalculationResult geometric,
            LegacyPlanMetricResult legacy)
        {
            if (geometric == null)
            {
                throw new ArgumentNullException("geometric");
            }

            if (legacy == null)
            {
                throw new ArgumentNullException("legacy");
            }
            Geometric = geometric;
            Legacy = legacy;
        }

        public BreastSurrogateCalculationResult Geometric { get; private set; }
        public LegacyPlanMetricResult Legacy { get; private set; }
    }

    /// <summary>
    /// Presentation-free read-only calculation of geometric and legacy metrics
    /// from the same explicitly supplied physics plan.
    /// </summary>
    public sealed class PhysicsPlanMetricService
    {
        public PhysicsPlanMetricResult Calculate(Patient patient, PlanSetup physicsPlan)
        {
            if (patient == null)
            {
                throw new ArgumentNullException("patient");
            }

            if (physicsPlan == null)
            {
                throw new ArgumentNullException("physicsPlan");
            }

            var context = new EsapiContext(patient, physicsPlan);
            BreastSurrogateCalculationResult geometric =
                new BreastSurrogateCalculationService().Calculate(context);

            LegacyPlanMetricResult legacy;
            if (physicsPlan.StructureSet == null)
            {
                legacy = MissingStructureSet();
            }
            else
            {
                List<Beam> antMed = physicsPlan.Beams
                    .Where(beam => !beam.IsSetupField)
                    .Where(beam => string.Equals(
                        beam.Id,
                        BreastSurrogateCalculationService.RequiredField1BeamId,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                VVector? referenceIsocentre = antMed.Count == 1
                    ? (VVector?)antMed[0].IsocenterPosition
                    : null;
                legacy = new LegacyStructureMetricService().Calculate(
                    physicsPlan.StructureSet.Structures,
                    referenceIsocentre);
            }

            return new PhysicsPlanMetricResult(geometric, legacy);
        }

        private static LegacyPlanMetricResult MissingStructureSet()
        {
            const string reason = "The physics plan has no structure set.";
            return new LegacyPlanMetricResult(
                MetricCalculationResult.Unavailable(
                    "ILF", null, MetricCalculationStatus.MissingData, reason),
                MetricCalculationResult.Unavailable(
                    "HIF", null, MetricCalculationStatus.MissingData, reason),
                null,
                null,
                null,
                null);
        }
    }
}
