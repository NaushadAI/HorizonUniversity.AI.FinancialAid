using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence;
using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Workflows;
using HorizonUniversity.AI.FinancialAid.Domain.Entities;
using HorizonUniversity.AI.FinancialAid.Domain.Enums;

namespace HorizonUniversity.AI.FinancialAid.Workflows
{
    public sealed class FinancialAidWorkflowHost(
    FinancialAidApprovalWorkflowFactory workflowFactory,
    IFinancialAidRepository repository) : IFinancialAidWorkflowHost
    {
        public async Task StartWorkflowAsync(WorkflowCaseState initialState, CancellationToken ct)
        {
            var runId = $"fa-{initialState.RequestId}";
            _ = workflowFactory.Build();

            await repository.UpdateStatusAndAuditAsync(
                initialState.RequestId,
                RequestStatus.UnderReview,
                "SYSTEM",
                "WORKFLOW",
                "ENTER_CSLO_REVIEW",
                null,
                "CSLO",
                notifyStudent: true,
                ct);

            await repository.CreateHumanTaskAsync(
                new HumanTaskDto(
                    TaskId: 0,
                    RequestId: initialState.RequestId,
                    WorkflowRunId: runId,
                    PortRequestId: "CSLO_STAGE",
                    GateName: "CSLO Review",
                    RequiredRole: "CSLO",
                    AllowedActions: ["ENDORSE", "REVERT"],
                    Summary: $"Review Financial Aid Request #{initialState.RequestId}"),
                ownerPidm: initialState.StudentPidm,
                ct);
        }

        public async Task ResumeGateAsync(HumanTaskDto task, WorkflowTaskResponse response, CancellationToken ct)
        {
            var status = response.Action == "ENDORSE"
                ? RequestStatus.PendingAssessment
                : RequestStatus.RevertedToStudent;

            await repository.UpdateStatusAndAuditAsync(
                task.RequestId,
                status,
                response.ActorRole,
                response.ActorId,
                response.Action,
                response.Comment,
                response.Action == "ENDORSE" ? "FAC_SECRETARY" : null,
                notifyStudent: true,
                ct);
        }
    }
}
