using Microsoft.Agents.AI.Workflows;
using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence;
using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Workflows;
using HorizonUniversity.AI.FinancialAid.Domain.Entities;
using HorizonUniversity.AI.FinancialAid.Domain.Enums;

namespace HorizonUniversity.AI.FinancialAid.Workflows;

public sealed record GateTaskRequest(
    long RequestId,
    long StudentPidm,
    string GateName,
    string RequiredRole,
    IReadOnlyList<string> AllowedActions,
    string Summary);

internal static class FinApproverRoles
{
    public const string Cslo = "CSLO";
}

public sealed class FinancialAidApprovalWorkflowFactory(IFinancialAidRepository repository)
{
    public Workflow Build()
    {
        Func<WorkflowCaseState, CancellationToken, ValueTask<GateTaskRequest>> csloHandler = async (state, ct) =>
        {
            await repository.UpdateStatusAndAuditAsync(
                state.RequestId, RequestStatus.UnderReview, "SYSTEM", "WORKFLOW",
                "ENTER_CSLO_REVIEW", null, FinApproverRoles.Cslo, notifyStudent: true, ct);

            return new GateTaskRequest(
                state.RequestId, state.StudentPidm, "CSLO Review", FinApproverRoles.Cslo,
                ["ENDORSE", "REVERT"], $"Review Financial Aid Request #{state.RequestId}");
        };
        var csloStage = csloHandler.BindAsExecutor<WorkflowCaseState, GateTaskRequest>("CSLO_STAGE");

        var builder = new WorkflowBuilder(csloStage)
            .WithName("HU-FinancialAid-Approval-v1")
            .WithDescription("Financial aid approval workflow entry point.");

        return builder.WithOutputFrom(csloStage).Build();
    }
}