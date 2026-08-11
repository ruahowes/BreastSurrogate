using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BreastSurrogate.Batch
{
    public enum AuditMetricType
    {
        MeanDose,
        VolumeAtDose,
        DoseAtVolume
    }

    public enum AuditVolumePresentation
    {
        None,
        RelativePercent,
        AbsoluteCc
    }

    public sealed class AuditMetricConfiguration
    {
        internal AuditMetricConfiguration(
            string name,
            string structure,
            AuditMetricType type,
            double? doseGy,
            double? volume,
            AuditVolumePresentation volumePresentation)
        {
            Name = name;
            Structure = structure;
            Type = type;
            DoseGy = doseGy;
            Volume = volume;
            VolumePresentation = volumePresentation;
        }

        public string Name { get; private set; }
        public string Structure { get; private set; }
        public AuditMetricType Type { get; private set; }
        public double? DoseGy { get; private set; }
        public double? Volume { get; private set; }
        public AuditVolumePresentation VolumePresentation { get; private set; }
    }

    public sealed class CourseDiscoveryConfiguration
    {
        internal CourseDiscoveryConfiguration(
            string planningCourseIdContains,
            bool requireRejectedPlan,
            int requiredReviewedPlanCount,
            string physicsCourseTokenPattern,
            string physicsPlanTokenPattern)
        {
            PlanningCourseIdContains = planningCourseIdContains;
            RequireRejectedPlan = requireRejectedPlan;
            RequiredReviewedPlanCount = requiredReviewedPlanCount;
            PhysicsCourseTokenPattern = physicsCourseTokenPattern;
            PhysicsPlanTokenPattern = physicsPlanTokenPattern;
        }

        public string PlanningCourseIdContains { get; private set; }
        public bool RequireRejectedPlan { get; private set; }
        public int RequiredReviewedPlanCount { get; private set; }
        public string PhysicsCourseTokenPattern { get; private set; }
        public string PhysicsPlanTokenPattern { get; private set; }
    }

    public sealed class BatchConfiguration
    {
        internal BatchConfiguration(
            int version,
            string hash,
            string logDirectory,
            string outputDirectory,
            CourseDiscoveryConfiguration courseDiscovery,
            double dvhBinWidthGy,
            IList<AuditMetricConfiguration> metrics)
        {
            Version = version;
            Hash = hash;
            LogDirectory = logDirectory;
            OutputDirectory = outputDirectory;
            CourseDiscovery = courseDiscovery;
            DvhBinWidthGy = dvhBinWidthGy;
            Metrics = new List<AuditMetricConfiguration>(metrics).AsReadOnly();
        }

        public int Version { get; private set; }
        public string Hash { get; private set; }
        public string LogDirectory { get; private set; }
        public string OutputDirectory { get; private set; }
        public CourseDiscoveryConfiguration CourseDiscovery { get; private set; }
        public double DvhBinWidthGy { get; private set; }
        public IList<AuditMetricConfiguration> Metrics { get; private set; }
    }

    public static class BatchConfigurationLoader
    {
        public const int SupportedVersion = 1;

        public static bool TryLoad(
            string path,
            out BatchConfiguration configuration,
            out string error)
        {
            configuration = null;
            error = null;

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                error = "Could not read JSON configuration: " + exception.Message;
                return false;
            }

            string json;
            try
            {
                json = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            }
            catch (DecoderFallbackException exception)
            {
                error = "JSON configuration is not valid UTF-8: " + exception.Message;
                return false;
            }

            string baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
            return TryParse(
                json,
                baseDirectory,
                ComputeHash(bytes),
                Directory.Exists,
                out configuration,
                out error);
        }

        internal static bool TryParse(
            string json,
            string baseDirectory,
            string hash,
            Func<string, bool> directoryExists,
            out BatchConfiguration configuration,
            out string error)
        {
            configuration = null;
            error = null;
            if (directoryExists == null)
            {
                throw new ArgumentNullException("directoryExists");
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON configuration is empty.";
                return false;
            }

            RawBatchConfiguration raw;
            try
            {
                var serializer = new DataContractJsonSerializer(
                    typeof(RawBatchConfiguration));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    raw = (RawBatchConfiguration)serializer.ReadObject(stream);
                }
            }
            catch (Exception exception)
            {
                error = "JSON configuration is invalid: " + exception.Message;
                return false;
            }

            if (raw == null || raw.Version != SupportedVersion)
            {
                error = "Configuration version must be "
                    + SupportedVersion.ToString(CultureInfo.InvariantCulture)
                    + ".";
                return false;
            }

            if (raw.Paths == null || string.IsNullOrWhiteSpace(raw.Paths.LogDirectory))
            {
                error = "paths.logDirectory is required.";
                return false;
            }

            string logDirectory;
            if (!TryResolveDirectory(
                raw.Paths.LogDirectory,
                baseDirectory,
                directoryExists,
                "Log",
                out logDirectory,
                out error))
            {
                return false;
            }

            string outputDirectory = logDirectory;
            if (!string.IsNullOrWhiteSpace(raw.Paths.OutputDirectory)
                && !TryResolveDirectory(
                    raw.Paths.OutputDirectory,
                    baseDirectory,
                    directoryExists,
                    "Output",
                    out outputDirectory,
                    out error))
            {
                return false;
            }

            CourseDiscoveryConfiguration discovery;
            if (!TryValidateDiscovery(raw.CourseDiscovery, out discovery, out error))
            {
                return false;
            }

            if (raw.Dvh == null
                || !IsFinite(raw.Dvh.BinWidthGy)
                || raw.Dvh.BinWidthGy <= 0.0)
            {
                error = "dvh.binWidthGy must be a finite positive number.";
                return false;
            }

            IList<AuditMetricConfiguration> metrics;
            if (!TryValidateMetrics(raw.Dvh.Metrics, out metrics, out error))
            {
                return false;
            }

            configuration = new BatchConfiguration(
                raw.Version,
                hash,
                logDirectory,
                outputDirectory,
                discovery,
                raw.Dvh.BinWidthGy,
                metrics);
            return true;
        }

        private static bool TryValidateDiscovery(
            RawCourseDiscovery raw,
            out CourseDiscoveryConfiguration discovery,
            out string error)
        {
            discovery = null;
            if (raw == null)
            {
                error = "courseDiscovery is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(raw.PlanningCourseIdContains))
            {
                error = "courseDiscovery.planningCourseIdContains is required.";
                return false;
            }

            if (!raw.RequireRejectedPlan || raw.RequiredReviewedPlanCount != 1)
            {
                error = "The initial audit requires a rejected plan and exactly one reviewed plan.";
                return false;
            }

            if (!IsValidRegex(raw.PhysicsCourseTokenPattern)
                || !IsValidRegex(raw.PhysicsPlanTokenPattern))
            {
                error = "Physics course and plan token patterns must be valid non-empty regular expressions.";
                return false;
            }

            discovery = new CourseDiscoveryConfiguration(
                raw.PlanningCourseIdContains.Trim(),
                raw.RequireRejectedPlan,
                raw.RequiredReviewedPlanCount,
                raw.PhysicsCourseTokenPattern,
                raw.PhysicsPlanTokenPattern);
            error = null;
            return true;
        }

        private static bool TryValidateMetrics(
            RawAuditMetric[] rawMetrics,
            out IList<AuditMetricConfiguration> metrics,
            out string error)
        {
            metrics = null;
            if (rawMetrics == null || rawMetrics.Length == 0)
            {
                error = "dvh.metrics must contain at least one metric.";
                return false;
            }

            var validated = new List<AuditMetricConfiguration>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < rawMetrics.Length; index++)
            {
                RawAuditMetric raw = rawMetrics[index];
                string prefix = "dvh.metrics[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (raw == null || string.IsNullOrWhiteSpace(raw.Name)
                    || !Regex.IsMatch(raw.Name, "^[A-Za-z][A-Za-z0-9_]*$"))
                {
                    error = prefix + ".name must contain only letters, digits and underscores and start with a letter.";
                    return false;
                }

                if (!names.Add(raw.Name))
                {
                    error = "Metric names must be unique (case-insensitive): " + raw.Name;
                    return false;
                }

                string structure = NormalizeStructure(raw.Structure);
                if (structure == null)
                {
                    error = prefix + ".structure must be IpsilateralLung or Heart.";
                    return false;
                }

                AuditMetricType type;
                if (!Enum.TryParse(raw.Type, true, out type))
                {
                    error = prefix + ".type is unsupported: " + (raw.Type ?? "<null>");
                    return false;
                }

                AuditVolumePresentation volumePresentation;
                if (!TryParseVolumePresentation(
                    raw.VolumePresentation,
                    out volumePresentation))
                {
                    error = prefix + ".volumePresentation is unsupported.";
                    return false;
                }

                if (type == AuditMetricType.MeanDose)
                {
                    if (!string.Equals(raw.DosePresentation, "AbsoluteGy", StringComparison.OrdinalIgnoreCase)
                        || raw.DoseGy.HasValue
                        || raw.Volume.HasValue
                        || volumePresentation != AuditVolumePresentation.None)
                    {
                        error = prefix + " MeanDose requires dosePresentation AbsoluteGy and no dose/volume query values.";
                        return false;
                    }
                }
                else if (type == AuditMetricType.VolumeAtDose)
                {
                    if (!IsFiniteNonNegative(raw.DoseGy)
                        || raw.Volume.HasValue
                        || volumePresentation == AuditVolumePresentation.None
                        || !string.IsNullOrWhiteSpace(raw.DosePresentation))
                    {
                        error = prefix + " VolumeAtDose requires a non-negative doseGy and a volumePresentation.";
                        return false;
                    }
                }
                else if (!IsFiniteNonNegative(raw.Volume)
                    || raw.DoseGy.HasValue
                    || volumePresentation == AuditVolumePresentation.None
                    || !string.Equals(raw.DosePresentation, "AbsoluteGy", StringComparison.OrdinalIgnoreCase))
                {
                    error = prefix + " DoseAtVolume requires a non-negative volume, volumePresentation and dosePresentation AbsoluteGy.";
                    return false;
                }

                validated.Add(new AuditMetricConfiguration(
                    raw.Name,
                    structure,
                    type,
                    raw.DoseGy,
                    raw.Volume,
                    volumePresentation));
            }

            metrics = validated;
            error = null;
            return true;
        }

        private static bool TryResolveDirectory(
            string configuredPath,
            string baseDirectory,
            Func<string, bool> directoryExists,
            string label,
            out string resolvedPath,
            out string error)
        {
            try
            {
                resolvedPath = Path.GetFullPath(
                    Path.IsPathRooted(configuredPath)
                        ? configuredPath
                        : Path.Combine(baseDirectory, configuredPath));
            }
            catch (Exception exception)
            {
                resolvedPath = null;
                error = label + " directory path is invalid: " + exception.Message;
                return false;
            }

            if (!directoryExists(resolvedPath))
            {
                error = label + " directory was not found: " + resolvedPath;
                return false;
            }

            error = null;
            return true;
        }

        private static string NormalizeStructure(string structure)
        {
            if (string.Equals(structure, "IpsilateralLung", StringComparison.OrdinalIgnoreCase))
            {
                return "IpsilateralLung";
            }

            return string.Equals(structure, "Heart", StringComparison.OrdinalIgnoreCase)
                ? "Heart"
                : null;
        }

        private static bool TryParseVolumePresentation(
            string value,
            out AuditVolumePresentation presentation)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                presentation = AuditVolumePresentation.None;
                return true;
            }

            return Enum.TryParse(value, true, out presentation)
                && presentation != AuditVolumePresentation.None;
        }

        private static bool IsValidRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return false;
            }

            try
            {
                new Regex(pattern);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool IsFiniteNonNegative(double? value)
        {
            return value.HasValue && IsFinite(value.Value) && value.Value >= 0.0;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string ComputeHash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte item in hash)
                {
                    text.Append(item.ToString("x2", CultureInfo.InvariantCulture));
                }

                return text.ToString();
            }
        }

        [DataContract]
        private sealed class RawBatchConfiguration
        {
            [DataMember(Name = "version")]
            public int Version { get; set; }

            [DataMember(Name = "paths")]
            public RawPaths Paths { get; set; }

            [DataMember(Name = "courseDiscovery")]
            public RawCourseDiscovery CourseDiscovery { get; set; }

            [DataMember(Name = "dvh")]
            public RawDvh Dvh { get; set; }
        }

        [DataContract]
        private sealed class RawPaths
        {
            [DataMember(Name = "logDirectory")]
            public string LogDirectory { get; set; }

            [DataMember(Name = "outputDirectory")]
            public string OutputDirectory { get; set; }
        }

        [DataContract]
        private sealed class RawCourseDiscovery
        {
            [DataMember(Name = "planningCourseIdContains")]
            public string PlanningCourseIdContains { get; set; }

            [DataMember(Name = "requireRejectedPlan")]
            public bool RequireRejectedPlan { get; set; }

            [DataMember(Name = "requiredReviewedPlanCount")]
            public int RequiredReviewedPlanCount { get; set; }

            [DataMember(Name = "physicsCourseTokenPattern")]
            public string PhysicsCourseTokenPattern { get; set; }

            [DataMember(Name = "physicsPlanTokenPattern")]
            public string PhysicsPlanTokenPattern { get; set; }
        }

        [DataContract]
        private sealed class RawDvh
        {
            [DataMember(Name = "binWidthGy")]
            public double BinWidthGy { get; set; }

            [DataMember(Name = "metrics")]
            public RawAuditMetric[] Metrics { get; set; }
        }

        [DataContract]
        private sealed class RawAuditMetric
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "structure")]
            public string Structure { get; set; }

            [DataMember(Name = "type")]
            public string Type { get; set; }

            [DataMember(Name = "doseGy")]
            public double? DoseGy { get; set; }

            [DataMember(Name = "volume")]
            public double? Volume { get; set; }

            [DataMember(Name = "volumePresentation")]
            public string VolumePresentation { get; set; }

            [DataMember(Name = "dosePresentation")]
            public string DosePresentation { get; set; }
        }
    }
}
