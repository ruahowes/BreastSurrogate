using System;
using BreastSurrogate.Core.Calculation;
using BreastSurrogate.Esapi.Esapi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Esapi.Tests.Calculation
{
    [TestClass]
    public class SurrogateMetricResultTests
    {
        [TestMethod]
        public void AvailableDerivesValueAndStructureFromSamplingResult()
        {
            StructureVoxelSamplingResult sampling = CreateSamplingResult("IPS LUNG");

            SurrogateMetricResult result = SurrogateMetricResult.Available(
                "gILF",
                sampling);

            Assert.AreEqual(SurrogateMetricStatus.Available, result.Status);
            Assert.IsTrue(result.IsAvailable);
            Assert.AreEqual("IPS LUNG", result.StructureId);
            Assert.AreEqual(10.0, result.Value.Value, 1e-12);
            Assert.AreEqual("%", result.Unit);
            Assert.IsNull(result.FailureReason);
            Assert.AreSame(sampling, result.SamplingResult);
        }

        [TestMethod]
        public void UnavailableRetainsReasonWithoutInventingValueOrUnit()
        {
            SurrogateMetricResult result = SurrogateMetricResult.Unavailable(
                "gHIF",
                null,
                "Heart was not present.");

            Assert.AreEqual(SurrogateMetricStatus.Unavailable, result.Status);
            Assert.IsFalse(result.IsAvailable);
            Assert.IsFalse(result.Value.HasValue);
            Assert.IsNull(result.Unit);
            Assert.AreEqual("Heart was not present.", result.FailureReason);
            Assert.IsNull(result.SamplingResult);
        }

        [TestMethod]
        public void FactoriesRejectMissingRequiredState()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => SurrogateMetricResult.Available("gILF", null));
            Assert.ThrowsException<ArgumentException>(
                () => SurrogateMetricResult.Unavailable("gILF", null, " "));
        }

        internal static StructureVoxelSamplingResult CreateSamplingResult(string structureId)
        {
            var accumulator = new InFieldSampleAccumulator();
            accumulator.Add(true, false);
            InFieldCalculationResult inField = accumulator.CreateResult(1000.0, 10.0);

            return new StructureVoxelSamplingResult(
                structureId,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                1,
                1000.0,
                10.0,
                inField,
                5);
        }
    }
}
