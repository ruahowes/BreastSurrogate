using System;
using BreastSurrogate.Core.Calculation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Core.Tests.Calculation
{
    [TestClass]
    public class InFieldSampleAccumulatorTests
    {
        [TestMethod]
        public void AddCountsFieldUnionAndIntersectionMembership()
        {
            var accumulator = new InFieldSampleAccumulator();
            accumulator.Add(false, false);
            accumulator.Add(true, false);
            accumulator.Add(false, true);
            accumulator.Add(true, true);

            InFieldCalculationResult result = accumulator.CreateResult(1000.0, 10.0);

            Assert.AreEqual(4L, result.TotalStructurePointCount);
            Assert.AreEqual(2L, result.Field1PointCount);
            Assert.AreEqual(2L, result.Field2PointCount);
            Assert.AreEqual(3L, result.EitherFieldPointCount);
            Assert.AreEqual(1L, result.BothFieldsPointCount);
        }

        [TestMethod]
        public void PercentagesUseEsapiVolumeRatherThanSampledPointCount()
        {
            var accumulator = new InFieldSampleAccumulator();
            accumulator.Add(false, false);
            accumulator.Add(true, false);
            accumulator.Add(false, true);
            accumulator.Add(true, true);

            InFieldCalculationResult result = accumulator.CreateResult(1000.0, 10.0);

            Assert.AreEqual(4.0, result.SampledStructureVolumeCubicCentimetres, 1e-12);
            Assert.AreEqual(2.0, result.Field1VolumeCubicCentimetres, 1e-12);
            Assert.AreEqual(2.0, result.Field2VolumeCubicCentimetres, 1e-12);
            Assert.AreEqual(3.0, result.EitherFieldVolumeCubicCentimetres, 1e-12);
            Assert.AreEqual(1.0, result.BothFieldsVolumeCubicCentimetres, 1e-12);
            Assert.AreEqual(20.0, result.Field1PercentageOfEsapiVolume, 1e-12);
            Assert.AreEqual(20.0, result.Field2PercentageOfEsapiVolume, 1e-12);
            Assert.AreEqual(30.0, result.EitherFieldPercentageOfEsapiVolume, 1e-12);
            Assert.AreEqual(10.0, result.BothFieldsPercentageOfEsapiVolume, 1e-12);
        }

        [TestMethod]
        public void CreateResultRejectsInvalidVolumes()
        {
            var accumulator = new InFieldSampleAccumulator();

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => accumulator.CreateResult(0.0, 1.0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => accumulator.CreateResult(1.0, double.NaN));
        }
    }
}
