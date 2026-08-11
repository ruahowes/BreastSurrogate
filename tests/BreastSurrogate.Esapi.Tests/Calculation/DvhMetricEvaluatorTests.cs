using System;
using BreastSurrogate.Esapi.Esapi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Esapi.Tests.Calculation
{
    [TestClass]
    public class DvhMetricEvaluatorTests
    {
        [TestMethod]
        public void MissingPlanDoseIsExplicitlyUnavailableWithoutQuery()
        {
            var source = new FakeSource { HasDoseValue = false };

            MetricCalculationResult result = Evaluate(
                source,
                Mean("Heart_Dmean_Gy"));

            Assert.AreEqual(MetricCalculationStatus.MissingData, result.Status);
            Assert.IsFalse(result.Value.HasValue);
            StringAssert.Contains(result.Reason, "no available dose");
            Assert.AreEqual(0, source.QueryCount);
        }

        [TestMethod]
        public void MeanDoseDispatchesCumulativeDvhAndConvertsCgyToGy()
        {
            var source = new FakeSource
            {
                MeanValue = DvhSourceValue.Available(
                    432.1,
                    DvhValueUnit.Centigray,
                    "cGy")
            };

            MetricCalculationResult result = Evaluate(
                source,
                Mean("Heart_Dmean_Gy"));

            Assert.IsTrue(result.IsAvailable, result.Reason);
            Assert.AreEqual(4.321, result.Value.Value, 1e-12);
            Assert.AreEqual("Gy", result.Unit);
            Assert.AreEqual("cGy", result.NativeDoseUnit);
            Assert.AreEqual(0.01, source.LastBinWidthGy, 0.0);
            Assert.AreEqual("MeanDose", source.LastQuery);
        }

        [TestMethod]
        public void VolumeAtDoseDispatchesRelativeAndAbsolutePresentations()
        {
            var relativeSource = new FakeSource
            {
                VolumeAtDoseValue = DvhSourceValue.Available(
                    12.7,
                    DvhValueUnit.RelativePercent,
                    "%")
            };
            MetricCalculationResult relative = Evaluate(
                relativeSource,
                new DvhMetricRequest(
                    "Lung_V8Gy_Percent",
                    DvhMetricKind.VolumeAtDose,
                    8.0,
                    null,
                    DvhVolumeKind.RelativePercent));

            Assert.AreEqual(12.7, relative.Value.Value, 0.0);
            Assert.AreEqual("%", relative.Unit);
            Assert.AreEqual(8.0, relativeSource.LastDoseGy, 0.0);
            Assert.AreEqual(DvhVolumeKind.RelativePercent, relativeSource.LastVolumeKind);

            var absoluteSource = new FakeSource
            {
                VolumeAtDoseValue = DvhSourceValue.Available(
                    24.5,
                    DvhValueUnit.AbsoluteCm3,
                    "cm3")
            };
            MetricCalculationResult absolute = Evaluate(
                absoluteSource,
                new DvhMetricRequest(
                    "Lung_V8Gy_Cc",
                    DvhMetricKind.VolumeAtDose,
                    8.0,
                    null,
                    DvhVolumeKind.AbsoluteCm3));

            Assert.AreEqual(24.5, absolute.Value.Value, 0.0);
            Assert.AreEqual("cc", absolute.Unit);
            Assert.AreEqual(DvhVolumeKind.AbsoluteCm3, absoluteSource.LastVolumeKind);
        }

        [TestMethod]
        public void DoseAtVolumeDispatchesRelativeAndAbsoluteVolumesAndNormalizesDose()
        {
            var relativeSource = new FakeSource
            {
                DoseAtVolumeValue = DvhSourceValue.Available(
                    7.5,
                    DvhValueUnit.Gy,
                    "Gy")
            };
            MetricCalculationResult relative = Evaluate(
                relativeSource,
                new DvhMetricRequest(
                    "Lung_D20Percent_Gy",
                    DvhMetricKind.DoseAtVolume,
                    null,
                    20.0,
                    DvhVolumeKind.RelativePercent));

            Assert.AreEqual(7.5, relative.Value.Value, 0.0);
            Assert.AreEqual(20.0, relativeSource.LastVolume, 0.0);
            Assert.AreEqual(DvhVolumeKind.RelativePercent, relativeSource.LastVolumeKind);

            var absoluteSource = new FakeSource
            {
                DoseAtVolumeValue = DvhSourceValue.Available(
                    650.0,
                    DvhValueUnit.Centigray,
                    "cGy")
            };
            MetricCalculationResult absolute = Evaluate(
                absoluteSource,
                new DvhMetricRequest(
                    "Heart_D10cc_Gy",
                    DvhMetricKind.DoseAtVolume,
                    null,
                    10.0,
                    DvhVolumeKind.AbsoluteCm3));

            Assert.AreEqual(6.5, absolute.Value.Value, 1e-12);
            Assert.AreEqual("Gy", absolute.Unit);
            Assert.AreEqual("cGy", absolute.NativeDoseUnit);
            Assert.AreEqual(DvhVolumeKind.AbsoluteCm3, absoluteSource.LastVolumeKind);
        }

        [TestMethod]
        public void NullOrUnavailableDvhNeverBecomesZero()
        {
            var nullSource = new FakeSource { MeanValue = null };
            MetricCalculationResult nullResult = Evaluate(nullSource, Mean("Mean"));
            Assert.AreEqual(MetricCalculationStatus.MissingData, nullResult.Status);
            Assert.IsFalse(nullResult.Value.HasValue);

            var unavailableSource = new FakeSource
            {
                MeanValue = DvhSourceValue.Unavailable(
                    MetricCalculationStatus.MissingData,
                    "ESAPI returned null DVH")
            };
            MetricCalculationResult unavailable = Evaluate(
                unavailableSource,
                Mean("Mean"));
            Assert.AreEqual(MetricCalculationStatus.MissingData, unavailable.Status);
            Assert.AreEqual("ESAPI returned null DVH", unavailable.Reason);
            Assert.IsFalse(unavailable.Value.HasValue);
        }

        [TestMethod]
        public void UnknownDoseUnitAndNonFiniteValueAreRejected()
        {
            var unknown = new FakeSource
            {
                MeanValue = DvhSourceValue.Available(
                    1.0,
                    DvhValueUnit.Unknown,
                    "%")
            };
            Assert.AreEqual(
                MetricCalculationStatus.Unsupported,
                Evaluate(unknown, Mean("Mean")).Status);

            var invalid = new FakeSource
            {
                MeanValue = DvhSourceValue.Available(
                    double.NaN,
                    DvhValueUnit.Gy,
                    "Gy")
            };
            Assert.AreEqual(
                MetricCalculationStatus.CalculationFailed,
                Evaluate(invalid, Mean("Mean")).Status);
        }

        [TestMethod]
        public void QueryExceptionIsMetricLevelCalculationFailure()
        {
            var source = new FakeSource { ExceptionToThrow = new InvalidOperationException("boom") };

            MetricCalculationResult result = Evaluate(source, Mean("Mean"));

            Assert.AreEqual(MetricCalculationStatus.CalculationFailed, result.Status);
            StringAssert.Contains(result.Reason, "boom");
        }

        private static DvhMetricRequest Mean(string name)
        {
            return new DvhMetricRequest(
                name,
                DvhMetricKind.MeanDose,
                null,
                null,
                DvhVolumeKind.None);
        }

        private static MetricCalculationResult Evaluate(
            FakeSource source,
            DvhMetricRequest request)
        {
            return new DvhMetricEvaluator().Evaluate(source, request, 0.01);
        }

        private sealed class FakeSource : IDvhDataSource
        {
            public FakeSource()
            {
                HasDoseValue = true;
                MeanValue = DvhSourceValue.Available(1.0, DvhValueUnit.Gy, "Gy");
                VolumeAtDoseValue = DvhSourceValue.Available(
                    1.0, DvhValueUnit.RelativePercent, "%");
                DoseAtVolumeValue = DvhSourceValue.Available(1.0, DvhValueUnit.Gy, "Gy");
            }

            public bool HasDoseValue { get; set; }
            public DvhSourceValue MeanValue { get; set; }
            public DvhSourceValue VolumeAtDoseValue { get; set; }
            public DvhSourceValue DoseAtVolumeValue { get; set; }
            public Exception ExceptionToThrow { get; set; }
            public int QueryCount { get; private set; }
            public string LastQuery { get; private set; }
            public double LastBinWidthGy { get; private set; }
            public double LastDoseGy { get; private set; }
            public double LastVolume { get; private set; }
            public DvhVolumeKind LastVolumeKind { get; private set; }

            public bool HasDose { get { return HasDoseValue; } }
            public string StructureId { get { return "Heart"; } }

            public DvhSourceValue GetMeanDose(double binWidthGy)
            {
                Record("MeanDose");
                LastBinWidthGy = binWidthGy;
                return MeanValue;
            }

            public DvhSourceValue GetVolumeAtDose(double doseGy, DvhVolumeKind volumeKind)
            {
                Record("VolumeAtDose");
                LastDoseGy = doseGy;
                LastVolumeKind = volumeKind;
                return VolumeAtDoseValue;
            }

            public DvhSourceValue GetDoseAtVolume(double volume, DvhVolumeKind volumeKind)
            {
                Record("DoseAtVolume");
                LastVolume = volume;
                LastVolumeKind = volumeKind;
                return DoseAtVolumeValue;
            }

            private void Record(string query)
            {
                QueryCount++;
                LastQuery = query;
                if (ExceptionToThrow != null) throw ExceptionToThrow;
            }
        }
    }
}
