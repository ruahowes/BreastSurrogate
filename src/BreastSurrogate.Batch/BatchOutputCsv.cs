using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BreastSurrogate.Batch
{
    public enum AuditValueStatus
    {
        Available,
        MissingData,
        Unsupported,
        Ambiguous,
        CalculationFailed,
        NotCalculated
    }

    public sealed class AuditMetricOutput
    {
        private AuditMetricOutput(
            double? value,
            string unit,
            AuditValueStatus status,
            string reason)
        {
            Value = value;
            Unit = unit;
            Status = status;
            Reason = reason;
        }

        public double? Value { get; private set; }
        public string Unit { get; private set; }
        public AuditValueStatus Status { get; private set; }
        public string Reason { get; private set; }

        public static AuditMetricOutput Available(double value, string unit)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException("value", "Metric value must be finite.");
            }

            if (string.IsNullOrWhiteSpace(unit))
            {
                throw new ArgumentException("Metric unit is required.", "unit");
            }

            return new AuditMetricOutput(value, unit, AuditValueStatus.Available, null);
        }

        public static AuditMetricOutput Unavailable(
            AuditValueStatus status,
            string reason)
        {
            if (status == AuditValueStatus.Available)
            {
                throw new ArgumentException("Unavailable status cannot be Available.", "status");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Unavailable metric reason is required.", "reason");
            }

            return new AuditMetricOutput(null, null, status, reason);
        }
    }

    public sealed class BatchOutputSchema
    {
        private static readonly string[] PrefixColumns =
        {
            "PatientId",
            "RequestedPlanningCourseId",
            "RequestedPhysicsCourseId",
            "RequestedPhysicsPlanId",
            "ResolvedPlanningCourseId",
            "ResolvedClinicalPlanId",
            "ResolvedPhysicsCourseId",
            "ResolvedPhysicsPlanId",
            "ClinicalDiscoveryStatus",
            "ClinicalDiscoveryMethod",
            "ClinicalDiscoveryReason",
            "PhysicsDiscoveryStatus",
            "PhysicsDiscoveryMethod",
            "PhysicsDiscoveryReason",
            "ClinicalIsocentreXmm",
            "ClinicalIsocentreYmm",
            "ClinicalIsocentreZmm",
            "ClinicalIsocentreReason",
            "Fractions",
            "ClinicalIpsilateralLungStructureId",
            "ClinicalHeartStructureId",
            "PhysicsIpsilateralLungStructureId",
            "PhysicsHeartStructureId",
            "ILFStructureId",
            "HIFStructureId"
        };

        private static readonly string[] SurrogateNames =
        {
            "gILF",
            "gHIF",
            "ILF",
            "HIF"
        };

        private static readonly string[] SuffixColumns =
        {
            "Warnings",
            "DiscoveryFailures",
            "ConfigurationVersion",
            "ConfigurationHash",
            "ApplicationVersion"
        };

        private BatchOutputSchema(IList<string> columns)
        {
            Columns = new List<string>(columns).AsReadOnly();
        }

        public IList<string> Columns { get; private set; }

        public static BatchOutputSchema Create(BatchConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            var columns = new List<string>(PrefixColumns);
            foreach (string name in SurrogateNames)
            {
                AddMetricColumns(columns, name, "ValuePercent");
            }

            foreach (AuditMetricConfiguration metric in configuration.Metrics)
            {
                AddMetricColumns(columns, metric.Name, "Value");
            }

            columns.AddRange(SuffixColumns);
            return new BatchOutputSchema(columns);
        }

        private static void AddMetricColumns(
            ICollection<string> columns,
            string name,
            string valueSuffix)
        {
            columns.Add(name + "_" + valueSuffix);
            columns.Add(name + "_Unit");
            columns.Add(name + "_Status");
            columns.Add(name + "_Reason");
        }
    }

    public sealed class BatchOutputRow
    {
        private readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public void SetText(string column, string value)
        {
            if (string.IsNullOrWhiteSpace(column))
            {
                throw new ArgumentException("Column name is required.", "column");
            }

            _values[column] = value ?? string.Empty;
        }

        public void SetInteger(string column, int? value)
        {
            SetText(
                column,
                value.HasValue
                    ? value.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);
        }

        public void SetMetric(string name, string valueSuffix, AuditMetricOutput metric)
        {
            if (metric == null)
            {
                throw new ArgumentNullException("metric");
            }

            string prefix = name + "_";
            SetText(
                prefix + valueSuffix,
                metric.Value.HasValue
                    ? metric.Value.Value.ToString("G17", CultureInfo.InvariantCulture)
                    : string.Empty);
            SetText(prefix + "Unit", metric.Unit);
            SetText(prefix + "Status", metric.Status.ToString());
            SetText(prefix + "Reason", metric.Reason);
        }

        internal string GetValue(string column)
        {
            string value;
            return _values.TryGetValue(column, out value) ? value : string.Empty;
        }

        internal IEnumerable<string> UnknownColumns(BatchOutputSchema schema)
        {
            var known = new HashSet<string>(schema.Columns, StringComparer.Ordinal);
            return _values.Keys.Where(column => !known.Contains(column));
        }
    }

    public sealed class BatchOutputCsvWriter
    {
        private readonly TextWriter _writer;
        private readonly BatchOutputSchema _schema;
        private bool _headerWritten;

        public BatchOutputCsvWriter(TextWriter writer, BatchOutputSchema schema)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            if (schema == null)
            {
                throw new ArgumentNullException("schema");
            }

            _writer = writer;
            _schema = schema;
        }

        public void WriteHeader()
        {
            if (_headerWritten)
            {
                throw new InvalidOperationException("CSV header has already been written.");
            }

            CsvCodec.WriteRecord(_writer, _schema.Columns);
            _writer.Flush();
            _headerWritten = true;
        }

        public void WriteRow(BatchOutputRow row)
        {
            if (!_headerWritten)
            {
                throw new InvalidOperationException("CSV header must be written first.");
            }

            if (row == null)
            {
                throw new ArgumentNullException("row");
            }

            string unknown = row.UnknownColumns(_schema).FirstOrDefault();
            if (unknown != null)
            {
                throw new InvalidOperationException("Output row contains an unknown column: " + unknown);
            }

            CsvCodec.WriteRecord(
                _writer,
                _schema.Columns.Select(row.GetValue));
            _writer.Flush();
        }
    }
}
