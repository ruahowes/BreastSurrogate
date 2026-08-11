using BreastSurrogate.Esapi.Esapi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Batch.Tests
{
    [TestClass]
    public class AuditDvhMetricRequestMapperTests
    {
        [TestMethod]
        public void MapsAllFiveSupportedMetricNotations()
        {
            ReviewedPlanMetricRequest mean = Map(new AuditMetricConfiguration(
                "Heart_Dmean_Gy", "Heart", AuditMetricType.MeanDose,
                null, null, AuditVolumePresentation.None));
            Assert.AreEqual("Heart", mean.StructureRole);
            Assert.AreEqual(DvhMetricKind.MeanDose, mean.Metric.Kind);

            ReviewedPlanMetricRequest vPercent = Map(new AuditMetricConfiguration(
                "Lung_V8Gy_Percent", "IpsilateralLung", AuditMetricType.VolumeAtDose,
                8.0, null, AuditVolumePresentation.RelativePercent));
            Assert.AreEqual(DvhMetricKind.VolumeAtDose, vPercent.Metric.Kind);
            Assert.AreEqual(DvhVolumeKind.RelativePercent, vPercent.Metric.VolumeKind);
            Assert.AreEqual(8.0, vPercent.Metric.DoseGy.Value, 0.0);

            ReviewedPlanMetricRequest vCc = Map(new AuditMetricConfiguration(
                "Lung_V8Gy_Cc", "IpsilateralLung", AuditMetricType.VolumeAtDose,
                8.0, null, AuditVolumePresentation.AbsoluteCc));
            Assert.AreEqual(DvhVolumeKind.AbsoluteCm3, vCc.Metric.VolumeKind);

            ReviewedPlanMetricRequest dCc = Map(new AuditMetricConfiguration(
                "Heart_D10cc_Gy", "Heart", AuditMetricType.DoseAtVolume,
                null, 10.0, AuditVolumePresentation.AbsoluteCc));
            Assert.AreEqual(DvhMetricKind.DoseAtVolume, dCc.Metric.Kind);
            Assert.AreEqual(DvhVolumeKind.AbsoluteCm3, dCc.Metric.VolumeKind);
            Assert.AreEqual(10.0, dCc.Metric.Volume.Value, 0.0);

            ReviewedPlanMetricRequest dPercent = Map(new AuditMetricConfiguration(
                "Lung_D20Percent_Gy", "IpsilateralLung", AuditMetricType.DoseAtVolume,
                null, 20.0, AuditVolumePresentation.RelativePercent));
            Assert.AreEqual(DvhVolumeKind.RelativePercent, dPercent.Metric.VolumeKind);
        }

        private static ReviewedPlanMetricRequest Map(AuditMetricConfiguration configuration)
        {
            return AuditDvhMetricRequestMapper.Create(configuration);
        }
    }
}
