using System;
using System.Collections.Generic;
using System.Linq;
using BreastSurrogate.Esapi.Esapi;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Batch
{
    public sealed class EsapiPatientAuditSource : IPatientAuditSource
    {
        private readonly Application _application;

        public EsapiPatientAuditSource(Application application)
        {
            if (application == null)
            {
                throw new ArgumentNullException("application");
            }

            _application = application;
        }

        public IPatientAuditSession OpenPatient(string patientId)
        {
            Patient patient = _application.OpenPatientById(patientId);
            if (patient == null)
            {
                throw new PatientNotFoundException(patientId);
            }

            return new EsapiPatientAuditSession(_application, patient);
        }
    }

    internal sealed class EsapiPatientAuditSession : IPatientAuditSession
    {
        private readonly Application _application;
        private readonly Patient _patient;
        private bool _disposed;

        public EsapiPatientAuditSession(Application application, Patient patient)
        {
            _application = application;
            _patient = patient;
        }

        public PatientDiscoverySnapshot CreateDiscoverySnapshot()
        {
            ThrowIfDisposed();
            return EsapiDiscoverySnapshotFactory.Create(_patient);
        }

        public PhysicsPlanMetricResult CalculatePhysics(string courseId, string planId)
        {
            ThrowIfDisposed();
            PlanSetup plan = ResolvePlan(courseId, planId);
            return new PhysicsPlanMetricService().Calculate(_patient, plan);
        }

        public ReviewedPlanMetricResult CalculateClinical(
            string courseId,
            string planId,
            DiscoveryPoint3D isocentre,
            IList<ReviewedPlanMetricRequest> requests,
            double binWidthGy)
        {
            ThrowIfDisposed();
            if (isocentre == null)
            {
                throw new ArgumentNullException("isocentre");
            }

            return new ReviewedPlanMetricService().Calculate(
                ResolvePlan(courseId, planId),
                new VVector(isocentre.X, isocentre.Y, isocentre.Z),
                requests,
                binWidthGy);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _application.ClosePatient();
        }

        private PlanSetup ResolvePlan(string courseId, string planId)
        {
            List<Course> courses = _patient.Courses.Where(course => string.Equals(
                course.Id,
                courseId,
                StringComparison.Ordinal)).ToList();
            if (courses.Count != 1)
            {
                throw new InvalidOperationException(
                    "Resolved course ID no longer identifies exactly one course: " + courseId);
            }

            List<ExternalPlanSetup> plans = courses[0].ExternalPlanSetups.Where(plan => string.Equals(
                plan.Id,
                planId,
                StringComparison.Ordinal)).ToList();
            if (plans.Count != 1)
            {
                throw new InvalidOperationException(
                    "Resolved plan ID no longer identifies exactly one external plan: " + planId);
            }

            return plans[0];
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("EsapiPatientAuditSession");
            }
        }
    }
}
