using BreastSurrogate.Esapi.Esapi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Esapi.Tests.Calculation
{
    [TestClass]
    public class BreastSurrogateCalculationResultTests
    {
        [TestMethod]
        public void StatusIsSuccessWhenBothMetricsAreAvailable()
        {
            BreastSurrogateCalculationResult result = CreateResult(
                Available("gILF", "IPS LUNG"),
                Available("gHIF", "Heart"),
                null);

            Assert.AreEqual(BreastSurrogateCalculationStatus.Success, result.Status);
        }

        [TestMethod]
        public void StatusIsPartialWhenOnlyOneMetricIsAvailable()
        {
            BreastSurrogateCalculationResult result = CreateResult(
                Available("gILF", "IPS LUNG"),
                SurrogateMetricResult.Unavailable("gHIF", null, "Heart absent."),
                null);

            Assert.AreEqual(
                BreastSurrogateCalculationStatus.PartialSuccess,
                result.Status);
            Assert.IsTrue(result.GeometricIlf.IsAvailable);
            Assert.IsFalse(result.GeometricHif.IsAvailable);
        }

        [TestMethod]
        public void StatusIsUnavailableWhenBothMetricsAreUnavailable()
        {
            const string reason = "Required beams were ambiguous.";
            BreastSurrogateCalculationResult result = CreateResult(
                SurrogateMetricResult.Unavailable("gILF", null, reason),
                SurrogateMetricResult.Unavailable("gHIF", null, reason),
                reason);

            Assert.AreEqual(
                BreastSurrogateCalculationStatus.Unavailable,
                result.Status);
            Assert.AreEqual(reason, result.SharedFailureReason);
        }

        private static BreastSurrogateCalculationResult CreateResult(
            SurrogateMetricResult geometricIlf,
            SurrogateMetricResult geometricHif,
            string sharedFailureReason)
        {
            return new BreastSurrogateCalculationResult(
                "PATIENT",
                "PPHYS",
                2,
                null,
                null,
                null,
                null,
                geometricIlf,
                geometricHif,
                sharedFailureReason);
        }

        private static SurrogateMetricResult Available(
            string metricName,
            string structureId)
        {
            return SurrogateMetricResult.Available(
                metricName,
                SurrogateMetricResultTests.CreateSamplingResult(structureId));
        }
    }
}
