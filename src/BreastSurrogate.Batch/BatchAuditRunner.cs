using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BreastSurrogate.Batch
{
    public sealed class BatchRunSummary
    {
        internal BatchRunSummary(int totalRows, int successfulRows, int rowsWithFailures)
        {
            TotalRows = totalRows;
            SuccessfulRows = successfulRows;
            RowsWithFailures = rowsWithFailures;
        }

        public int TotalRows { get; private set; }
        public int SuccessfulRows { get; private set; }
        public int RowsWithFailures { get; private set; }
    }

    public sealed class BatchAuditRunner
    {
        public BatchRunSummary Run(
            IList<PatientInputRow> patientRows,
            BatchConfiguration configuration,
            string applicationVersion,
            IPatientAuditSource patientSource,
            BatchOutputCsvWriter output,
            TextWriter batchLog,
            Action<int, PatientInputRow, IList<string>> writePatientLog,
            ConsoleProgressReporter progress)
        {
            if (patientRows == null) throw new ArgumentNullException("patientRows");
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (patientSource == null) throw new ArgumentNullException("patientSource");
            if (output == null) throw new ArgumentNullException("output");
            if (batchLog == null) throw new ArgumentNullException("batchLog");
            if (progress == null) throw new ArgumentNullException("progress");
            if (patientRows.Count == 0) throw new ArgumentException("No patient rows were supplied.");

            var processor = new PatientAuditProcessor();
            int rowsWithFailures = 0;
            output.WriteHeader();
            batchLog.WriteLine("BreastSurrogate batch audit");
            batchLog.WriteLine("Rows: " + patientRows.Count.ToString(CultureInfo.InvariantCulture));
            batchLog.WriteLine("Configuration version: "
                + configuration.Version.ToString(CultureInfo.InvariantCulture));
            batchLog.WriteLine("Configuration hash: " + configuration.Hash);
            batchLog.Flush();
            progress.Report(0, patientRows.Count, "Ready");

            for (int index = 0; index < patientRows.Count; index++)
            {
                PatientInputRow input = patientRows[index];
                IPatientAuditSession session = null;
                PatientAuditResult result = null;
                progress.Report(index, patientRows.Count, "Opening " + input.PatientId);
                try
                {
                    session = patientSource.OpenPatient(input.PatientId);
                    result = processor.Process(
                        input,
                        configuration,
                        applicationVersion,
                        session);
                }
                catch (PatientNotFoundException exception)
                {
                    result = processor.CreateFailure(
                        input,
                        configuration,
                        applicationVersion,
                        AuditValueStatus.MissingData,
                        exception.Message);
                }
                catch (Exception exception)
                {
                    result = processor.CreateFailure(
                        input,
                        configuration,
                        applicationVersion,
                        AuditValueStatus.CalculationFailed,
                        "Patient processing failed: " + exception.Message);
                    result.AddLogLine("Exception type: " + exception.GetType().FullName);
                }
                finally
                {
                    if (session != null)
                    {
                        try
                        {
                            session.Dispose();
                            if (result != null)
                            {
                                result.AddLogLine("Patient closed successfully.");
                            }
                        }
                        catch (Exception exception)
                        {
                            if (result == null)
                            {
                                result = processor.CreateFailure(
                                    input,
                                    configuration,
                                    applicationVersion,
                                    AuditValueStatus.CalculationFailed,
                                    "Patient closure failed: " + exception.Message);
                            }
                            else
                            {
                                result.AddWarning("Patient closure failed: " + exception.Message);
                            }
                        }
                    }
                }

                if (writePatientLog != null)
                {
                    try
                    {
                        writePatientLog(index, input, result.LogLines);
                    }
                    catch (Exception exception)
                    {
                        result.AddWarning("Per-patient log write failed: " + exception.Message);
                    }
                }

                output.WriteRow(result.Row);
                WriteBatchLogSection(batchLog, index, result);
                if (result.HasFailures)
                {
                    rowsWithFailures++;
                }

                progress.Report(
                    index + 1,
                    patientRows.Count,
                    input.PatientId + (result.HasFailures ? " completed with unavailable values" : " complete"));
            }

            int successful = patientRows.Count - rowsWithFailures;
            batchLog.WriteLine();
            batchLog.WriteLine("Batch summary");
            batchLog.WriteLine("Total rows: " + patientRows.Count.ToString(CultureInfo.InvariantCulture));
            batchLog.WriteLine("Rows fully available: " + successful.ToString(CultureInfo.InvariantCulture));
            batchLog.WriteLine("Rows with unavailable values: " + rowsWithFailures.ToString(CultureInfo.InvariantCulture));
            batchLog.Flush();
            return new BatchRunSummary(patientRows.Count, successful, rowsWithFailures);
        }

        private static void WriteBatchLogSection(
            TextWriter batchLog,
            int index,
            PatientAuditResult result)
        {
            batchLog.WriteLine();
            batchLog.WriteLine("Patient row " + (index + 1).ToString(CultureInfo.InvariantCulture));
            foreach (string line in result.LogLines)
            {
                batchLog.WriteLine(line);
            }

            batchLog.Flush();
        }
    }
}
