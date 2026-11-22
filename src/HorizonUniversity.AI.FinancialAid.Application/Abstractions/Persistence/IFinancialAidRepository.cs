using HorizonUniversity.AI.FinancialAid.Domain.Entities;
using HorizonUniversity.AI.FinancialAid.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence
{
    public interface IFinancialAidRepository
    {
        Task<bool> HasOpenRequestAsync(long pidm, CancellationToken ct);
        Task<IReadOnlyList<FinancialAidRequestSummary>> GetRequestsByPidmAsync(long pidm, CancellationToken ct);
        Task SaveDraftAsync(string sessionId, long pidm, FinancialAidDraft draft, CancellationToken ct);
        Task<FinancialAidDraft?> GetPreparedDraftAsync(string sessionId, long pidm, CancellationToken ct);
        Task<long> SubmitDraftAndEnqueueWorkflowAsync(string sessionId, long pidm, CancellationToken ct);

        Task<string?> LoadChatSessionJsonAsync(string sessionId, long pidm, CancellationToken ct);
        Task SaveChatSessionJsonAsync(string sessionId, long pidm, string sessionJson, CancellationToken ct);

        Task<IReadOnlyList<HumanTaskDto>> GetOpenTasksForRoleAsync(string role, CancellationToken ct);
        Task<HumanTaskDto?> TryClaimTaskAsync(long taskId, string callerRole, long callerPidm, string action, CancellationToken ct);
        Task MarkTaskDoneAsync(long taskId, CancellationToken ct);
        Task ReleaseClaimedTaskAsync(long taskId, CancellationToken ct);

        Task UpdateStatusAndAuditAsync(
            long requestId,
            RequestStatus newStatus,
            string actorRole,
            string actorId,
            string action,
            string? comment,
            string? notifyRole,
            bool notifyStudent,
            CancellationToken ct);

        Task<string?> LoadCheckpointJsonAsync(string runId, CancellationToken ct);
        Task SaveCheckpointJsonAsync(string runId, string checkpointId, string? parentId, string payloadJson, CancellationToken ct);
        Task CreateHumanTaskAsync(HumanTaskDto task, long ownerPidm, CancellationToken ct);

        Task<IReadOnlyList<OutboxMessage>> ClaimPendingOutboxAsync(int batchSize, CancellationToken ct);
        Task MarkOutboxProcessedAsync(long outboxId, CancellationToken ct);
        Task MarkOutboxFailedAsync(long outboxId, string error, CancellationToken ct);
    }
}
