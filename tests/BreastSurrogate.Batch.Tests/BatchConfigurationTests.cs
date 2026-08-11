using System;
using System.IO;
using BreastSurrogate.Batch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreastSurrogate.Batch.Tests
{
    [TestClass]
    public class BatchConfigurationTests
    {
        [TestMethod]
        public void ValidConfigurationResolvesDirectoriesAndInitialMetrics()
        {
            BatchConfiguration configuration;
            string error;

            bool loaded = BatchConfigurationLoader.TryParse(
                ValidJson,
                Path.GetFullPath("config-root"),
                "abc123",
                path => true,
                out configuration,
                out error);

            Assert.IsTrue(loaded, error);
            Assert.AreEqual(1, configuration.Version);
            Assert.AreEqual("abc123", configuration.Hash);
            Assert.AreEqual(3, configuration.Metrics.Count);
            Assert.AreEqual(AuditMetricType.VolumeAtDose, configuration.Metrics[0].Type);
            Assert.AreEqual(AuditVolumePresentation.RelativePercent,
                configuration.Metrics[0].VolumePresentation);
            Assert.AreEqual(configuration.LogDirectory, configuration.OutputDirectory);
        }

        [TestMethod]
        public void InvalidJsonAndVersionAreRejected()
        {
            BatchConfiguration configuration;
            string error;

            Assert.IsFalse(BatchConfigurationLoader.TryParse(
                "{broken",
                Path.GetFullPath("."),
                "hash",
                path => true,
                out configuration,
                out error));
            StringAssert.Contains(error, "invalid");

            Assert.IsFalse(BatchConfigurationLoader.TryParse(
                ValidJson.Replace("\"version\": 1", "\"version\": 2"),
                Path.GetFullPath("."),
                "hash",
                path => true,
                out configuration,
                out error));
            StringAssert.Contains(error, "version");
        }

        [TestMethod]
        public void InvalidMetricRequestIsRejectedClearly()
        {
            BatchConfiguration configuration;
            string error;
            string json = ValidJson.Replace(
                "\"VolumeAtDose\"",
                "\"UnknownMetric\"");

            Assert.IsFalse(BatchConfigurationLoader.TryParse(
                json,
                Path.GetFullPath("."),
                "hash",
                path => true,
                out configuration,
                out error));
            StringAssert.Contains(error, "unsupported");
        }

        [TestMethod]
        public void MissingConfiguredDirectoryIsRejected()
        {
            BatchConfiguration configuration;
            string error;

            Assert.IsFalse(BatchConfigurationLoader.TryParse(
                ValidJson,
                Path.GetFullPath("."),
                "hash",
                path => false,
                out configuration,
                out error));
            StringAssert.Contains(error, "not found");
        }

        [TestMethod]
        public void AbsoluteVolumeMetricFormsAreAccepted()
        {
            string json = ValidJson.Replace(
                @"""type"": ""VolumeAtDose"",
        ""doseGy"": 8.0,
        ""volumePresentation"": ""RelativePercent""",
                @"""type"": ""DoseAtVolume"",
        ""volume"": 2.0,
        ""volumePresentation"": ""AbsoluteCc"",
        ""dosePresentation"": ""AbsoluteGy""");
            BatchConfiguration configuration;
            string error;

            Assert.IsTrue(BatchConfigurationLoader.TryParse(
                json,
                Path.GetFullPath("."),
                "hash",
                path => true,
                out configuration,
                out error), error);
            Assert.AreEqual(AuditMetricType.DoseAtVolume, configuration.Metrics[0].Type);
            Assert.AreEqual(AuditVolumePresentation.AbsoluteCc,
                configuration.Metrics[0].VolumePresentation);
            Assert.AreEqual(2.0, configuration.Metrics[0].Volume.Value, 0.0);
        }

        internal const string ValidJson = @"{
  ""version"": 1,
  ""paths"": { ""logDirectory"": ""logs"" },
  ""courseDiscovery"": {
    ""planningCourseIdContains"": ""PLANNING"",
    ""requireRejectedPlan"": true,
    ""requiredReviewedPlanCount"": 1,
    ""physicsCourseTokenPattern"": ""(?:^|[ _-])(PPHYS|PHYS)(?:$|[ _-])"",
    ""physicsPlanTokenPattern"": ""(?:^|[ _-])(PPHYS|PHYS)(?:$|[ _-])""
  },
  ""dvh"": {
    ""binWidthGy"": 0.01,
    ""metrics"": [
      {
        ""name"": ""IpsilateralLung_V8Gy_Percent"",
        ""structure"": ""IpsilateralLung"",
        ""type"": ""VolumeAtDose"",
        ""doseGy"": 8.0,
        ""volumePresentation"": ""RelativePercent""
      },
      {
        ""name"": ""IpsilateralLung_V12Gy_Percent"",
        ""structure"": ""IpsilateralLung"",
        ""type"": ""VolumeAtDose"",
        ""doseGy"": 12.0,
        ""volumePresentation"": ""RelativePercent""
      },
      {
        ""name"": ""Heart_Dmean_Gy"",
        ""structure"": ""Heart"",
        ""type"": ""MeanDose"",
        ""dosePresentation"": ""AbsoluteGy""
      }
    ]
  }
}";
    }
}
