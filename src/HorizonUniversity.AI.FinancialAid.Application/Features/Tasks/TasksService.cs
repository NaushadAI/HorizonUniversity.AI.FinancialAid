using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence;
using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Workflows;
using HorizonUniversity.AI.FinancialAid.Application.DTOs;
using HorizonUniversity.AI.FinancialAid.Domain.Entities;

namespace HorizonUniversity.AI.FinancialAid.Application.Features.Tasks;

public sealed class TasksService(
    IFinancialAidRepository repository,
    IFinancialAidWorkflowHost workflowHost)
{
    public Task<IReadOnlyList<HumanTaskDto>> GetOpenTasksAsync(string role, CancellationToken ct)
        => repository.GetOpenTasksForRoleAsync(role, ct);

    public async Task<(bool Claimed, string? Error)> CompleteTaskAsync(
        long taskId,
        CompleteTaskCommand cmd,
        string callerRole,
        long callerPidm,
        string actorId,
        CancellationToken ct)
    {
        var claimed = await repository.TryClaimTaskAsync(taskId, callerRole, callerPidm, cmd.Action, ct);
        if (claimed is null)
            return (false, "Task is unavailable, already claimed, or action is not allowed.");

        try
        {
            await workflowHost.ResumeGateAsync(
                claimed,
                new WorkflowTaskResponse(cmd.Action, callerRole, actorId, cmd.Comment),
                ct);

            await repository.MarkTaskDoneAsync(taskId, ct);
            return (true, null);
        }
        catch
        {
            await repository.ReleaseClaimedTaskAsync(taskId, ct);
            throw;
        }
    }
}