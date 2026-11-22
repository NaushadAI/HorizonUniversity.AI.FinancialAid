using HorizonUniversity.AI.FinancialAid.Domain.Entities;
using HorizonUniversity.AI.FinancialAid.Domain.Enums;

namespace HorizonUniversity.AI.FinancialAid.Application.Abstractions.Workflows
{
    public sealed record WorkflowCaseState(
    long RequestId,
    long StudentPidm,
    AidType AidType,
    string CurrentStage,
    string? LastAction = null,
    string? LastComment = null);

    public sealed record WorkflowTaskResponse(
        string Action,
        string ActorRole,
        string ActorId,
        string? Comment);

    public interface IFinancialAidWorkflowHost
    {
        Task StartWorkflowAsync(WorkflowCaseState initialState, CancellationToken ct);
        Task ResumeGateAsync(HumanTaskDto task, WorkflowTaskResponse response, CancellationToken ct);
    }
}
