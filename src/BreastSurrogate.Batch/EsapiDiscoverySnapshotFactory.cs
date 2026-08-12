using System;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Batch
{
    public static class EsapiDiscoverySnapshotFactory
    {
        public static PatientDiscoverySnapshot Create(Patient patient)
        {
            if (patient == null)
            {
                throw new ArgumentNullException("patient");
            }

            return new PatientDiscoverySnapshot(patient.Courses.Select(course =>
                new CourseDiscoverySnapshot(
                    course.Id,
                    course.ExternalPlanSetups.Select(plan =>
                        new ExternalPlanDiscoverySnapshot(
                            plan.Id,
                            MapApproval(plan.ApprovalStatus),
                            plan.Beams
                                .Where(beam => !beam.IsSetupField)
                                .Select(beam => new DiscoveryPoint3D(
                                    beam.IsocenterPosition.x,
                                    beam.IsocenterPosition.y,
                                    beam.IsocenterPosition.z)))))));
        }

        private static PlanApprovalKind MapApproval(PlanSetupApprovalStatus approval)
        {
            if (approval == PlanSetupApprovalStatus.Rejected)
            {
                return PlanApprovalKind.Rejected;
            }

            if (approval == PlanSetupApprovalStatus.PlanningApproved)
            {
                return PlanApprovalKind.PlanningApproved;
            }

            return approval == PlanSetupApprovalStatus.Reviewed
                ? PlanApprovalKind.Reviewed
                : PlanApprovalKind.Other;
        }
    }
}
