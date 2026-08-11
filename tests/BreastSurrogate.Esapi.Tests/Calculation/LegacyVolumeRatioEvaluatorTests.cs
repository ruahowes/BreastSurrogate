using BreastSurrogate.Esapi.Esapi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Esapi.Tests.Calculation
{
    [TestClass]
    public class LegacyVolumeRatioEvaluatorTests
    {
        [TestMethod]
        public void CalculatesPercentageFromEsapiStructureVolumes()
        {
            MetricCalculationResult result = LegacyVolumeRatioEvaluator.Evaluate(
                "ILF", "ILF 12.7%", 127.0, "IPS LUNG", 1000.0);

            Assert.IsTrue(result.IsAvailable, result.Reason);
            Assert.AreEqual(12.7, result.Value.Value, 1e-12);
            Assert.AreEqual("%", result.Unit);
            Assert.AreEqual("ILF 12.7%", result.StructureId);
        }

        [TestMethod]
        public void MissingOrInvalidDenominatorIsUnavailableNotZero()
        {
            MetricCalculationResult missing = LegacyVolumeRatioEvaluator.Evaluate(
                "HIF", "HIF", 1.0, null, 100.0);
            Assert.AreEqual(MetricCalculationStatus.MissingData, missing.Status);
            Assert.IsFalse(missing.Value.HasValue);

            MetricCalculationResult zero = LegacyVolumeRatioEvaluator.Evaluate(
                "HIF", "HIF", 1.0, "Heart", 0.0);
            Assert.AreEqual(MetricCalculationStatus.CalculationFailed, zero.Status);
            Assert.IsFalse(zero.Value.HasValue);
        }

        [TestMethod]
        public void InvalidNumeratorVolumeIsCalculationFailure()
        {
            MetricCalculationResult result = LegacyVolumeRatioEvaluator.Evaluate(
                "ILF", "ILF", double.NaN, "IPS LUNG", 100.0);

            Assert.AreEqual(MetricCalculationStatus.CalculationFailed, result.Status);
            Assert.IsFalse(result.Value.HasValue);
        }
    }
}
