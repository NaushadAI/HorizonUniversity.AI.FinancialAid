 
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence;
using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Workflows;
using HorizonUniversity.AI.FinancialAid.Domain.Entities;
using HorizonUniversity.AI.FinancialAid.Domain.Enums;
using System.Data;


namespace HorizonUniversity.AI.FinancialAid.Infrastructure.SqlServer.Repositories;

public sealed class SqlServerFinancialAidRepository(IConfiguration configuration) : IFinancialAidRepository
{
    private readonly string _connectionString =
        configuration.GetConnectionString("SqlServerStuHu")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:SqlServerStuHu configuration.");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task<bool> HasOpenRequestAsync(long pidm, CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.STUHU_FIN_AID_REQUESTS
            WHERE PIDM = @Pidm
              AND STATUS NOT IN ('Rejected', 'CashAidReceived', 'LaptopReceived', 'AssistanceOthersReceived')
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var count = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { Pidm = pidm }, cancellationToken: ct));

        return count > 0;
    }

    public async Task<IReadOnlyList<FinancialAidRequestSummary>> GetRequestsByPidmAsync(long pidm, CancellationToken ct)
    {
        const string sql = """
            SELECT
                REQUEST_ID   AS RequestId,
                AID_TYPE     AS AidTypeRaw,
                STATUS       AS StatusRaw,
                CREATED_AT   AS CreatedAt,
                LAST_COMMENT AS LastComment
            FROM dbo.STUHU_FIN_AID_REQUESTS
            WHERE PIDM = @Pidm
            ORDER BY CREATED_AT DESC
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<RequestRow>(
            new CommandDefinition(sql, new { Pidm = pidm }, cancellationToken: ct));

        return rows.Select(r =>
        {
            Enum.TryParse<AidType>(r.AidTypeRaw, true, out var aidType);
            Enum.TryParse<RequestStatus>(r.StatusRaw, true, out var status);
            return new FinancialAidRequestSummary(
                RequestId: r.RequestId,
                AidType: aidType,
                Status: status,
                CreatedAt: r.CreatedAt,
                LastComment: r.LastComment);
        }).ToList();
    }

    public async Task SaveDraftAsync(string sessionId, long pidm, FinancialAidDraft draft, CancellationToken ct)
    {
        var draftJson = JsonSerializer.Serialize(draft, JsonOptions);

        const string sql = """
            UPDATE dbo.SZRFA_CHAT_SESSION
            SET DRAFT_JSON = @DraftJson, UPDATED_AT = SYSDATETIMEOFFSET()
            WHERE SESSION_ID = @SessionId AND OWNER_PIDM = @OwnerPidm;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.SZRFA_CHAT_SESSION (SESSION_ID, OWNER_PIDM, SESSION_JSON, DRAFT_JSON, UPDATED_AT)
                VALUES (@SessionId, @OwnerPidm, N'{}', @DraftJson, SYSDATETIMEOFFSET());
            END
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { SessionId = sessionId, OwnerPidm = pidm, DraftJson = draftJson },
            cancellationToken: ct));
    }

    public async Task<FinancialAidDraft?> GetPreparedDraftAsync(string sessionId, long pidm, CancellationToken ct)
    {
        const string sql = """
            SELECT DRAFT_JSON
            FROM dbo.SZRFA_CHAT_SESSION
            WHERE SESSION_ID = @SessionId
              AND OWNER_PIDM = @OwnerPidm
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var draftJson = await conn.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, new { SessionId = sessionId, OwnerPidm = pidm }, cancellationToken: ct));

        return string.IsNullOrWhiteSpace(draftJson) || draftJson == "{}"
            ? null
            : JsonSerializer.Deserialize<FinancialAidDraft>(draftJson, JsonOptions);
    }

    public async Task<long> SubmitDraftAndEnqueueWorkflowAsync(string sessionId, long pidm, CancellationToken ct)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            const string selectDraftSql = """
                SELECT DRAFT_JSON
                FROM dbo.SZRFA_CHAT_SESSION WITH (UPDLOCK, ROWLOCK)
                WHERE SESSION_ID = @SessionId
                  AND OWNER_PIDM = @OwnerPidm
                """;

            var draftJson = await conn.ExecuteScalarAsync<string?>(
                new CommandDefinition(selectDraftSql, new { SessionId = sessionId, OwnerPidm = pidm }, tx, cancellationToken: ct));

            if (string.IsNullOrWhiteSpace(draftJson) || draftJson == "{}")
                throw new InvalidOperationException("No prepared financial aid draft found for this session.");

            var draft = JsonSerializer.Deserialize<FinancialAidDraft>(draftJson, JsonOptions)
                ?? throw new InvalidOperationException("Prepared draft payload is invalid.");

            const string insertRequestSql = """
                INSERT INTO dbo.STUHU_FIN_AID_REQUESTS (
                    PIDM, AID_TYPE, OTHER_AID_DESC, FAMILY_MEMBERS_CNT,
                    HAS_SIBLINGS_AT_HU, IS_MOCE_RECIPIENT,
                    FATHER_SALARY, FATHER_NATIONALITY, FATHER_WORKPLACE, FATHER_JOB_ROLE,
                    MOTHER_SALARY, MOTHER_NATIONALITY, MOTHER_WORKPLACE, MOTHER_JOB_ROLE,
                    JUSTIFICATION, STATUS, CREATED_AT
                )
                OUTPUT INSERTED.REQUEST_ID
                VALUES (
                    @Pidm, @AidType, @OtherDesc, @FamilyMembers,
                    @HasSiblings, @IsMoce,
                    @FatherSalary, @FatherNat, @FatherWork, @FatherRole,
                    @MotherSalary, @MotherNat, @MotherWork, @MotherRole,
                    @Justification, @Status, SYSDATETIMEOFFSET()
                )
                """;

            var requestId = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                insertRequestSql,
                new
                {
                    Pidm = draft.Pidm,
                    AidType = draft.AidType.ToString(),
                    OtherDesc = draft.OtherAidDescription,
                    FamilyMembers = draft.FamilyMembersCount,
                    HasSiblings = draft.HasSiblingsAtHu ? "Y" : "N",
                    IsMoce = draft.IsMoceRecipient ? "Y" : "N",
                    FatherSalary = draft.Father.MonthlySalary,
                    FatherNat = draft.Father.Nationality,
                    FatherWork = draft.Father.Workplace,
                    FatherRole = draft.Father.JobRole,
                    MotherSalary = draft.Mother.MonthlySalary,
                    MotherNat = draft.Mother.Nationality,
                    MotherWork = draft.Mother.Workplace,
                    MotherRole = draft.Mother.JobRole,
                    draft.Justification,
                    Status = nameof(RequestStatus.UnderReview)
                },
                tx,
                cancellationToken: ct));

            const string insertAuditSql = """
                INSERT INTO dbo.SZRFA_AUDIT (
                    REQUEST_ID, ACTOR_ROLE, ACTOR_ID, ACTION, NEW_STATUS, COMMENT_TEXT, CREATED_AT
                ) VALUES (
                    @RequestId, @ActorRole, @ActorId, @Action, @NewStatus, @CommentText, SYSDATETIMEOFFSET()
                )
                """;

            await conn.ExecuteAsync(new CommandDefinition(
                insertAuditSql,
                new
                {
                    RequestId = requestId,
                    ActorRole = FinAidApproverRoles.Student,
                    ActorId = pidm.ToString(),
                    Action = "SUBMIT",
                    NewStatus = nameof(RequestStatus.UnderReview),
                    CommentText = "Submitted via Intake Agent"
                },
                tx,
                cancellationToken: ct));

            var workflowPayload = JsonSerializer.Serialize(
                new WorkflowCaseState(requestId, draft.Pidm, draft.AidType, "INITIAL"),
                JsonOptions);

            const string insertOutboxSql = """
                IF NOT EXISTS (
                    SELECT 1 FROM dbo.SZRFA_OUTBOX WHERE KIND = 'START_WORKFLOW' AND DEDUP_KEY = @DedupKey
                )
                BEGIN
                    INSERT INTO dbo.SZRFA_OUTBOX (KIND, DEDUP_KEY, PAYLOAD_JSON, STATUS, ATTEMPTS, CREATED_AT)
                    VALUES ('START_WORKFLOW', @DedupKey, @PayloadJson, 'PENDING', 0, SYSDATETIMEOFFSET());
                END
                """;

            await conn.ExecuteAsync(new CommandDefinition(
                insertOutboxSql,
                new { DedupKey = $"start-wf-{requestId}", PayloadJson = workflowPayload },
                tx,
                cancellationToken: ct));

            const string clearDraftSql = """
                UPDATE dbo.SZRFA_CHAT_SESSION
                SET DRAFT_JSON = NULL, UPDATED_AT = SYSDATETIMEOFFSET()
                WHERE SESSION_ID = @SessionId AND OWNER_PIDM = @OwnerPidm
                """;

            await conn.ExecuteAsync(new CommandDefinition(
                clearDraftSql,
                new { SessionId = sessionId, OwnerPidm = pidm },
                tx,
                cancellationToken: ct));

            await tx.CommitAsync(ct);
            return requestId;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<string?> LoadChatSessionJsonAsync(string sessionId, long pidm, CancellationToken ct)
    {
        const string sql = """
            SELECT SESSION_JSON
            FROM dbo.SZRFA_CHAT_SESSION
            WHERE SESSION_ID = @SessionId
              AND OWNER_PIDM = @OwnerPidm
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        return await conn.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, new { SessionId = sessionId, OwnerPidm = pidm }, cancellationToken: ct));
    }

    public async Task SaveChatSessionJsonAsync(string sessionId, long pidm, string sessionJson, CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.SZRFA_CHAT_SESSION
            SET SESSION_JSON = @SessionJson, UPDATED_AT = SYSDATETIMEOFFSET()
            WHERE SESSION_ID = @SessionId AND OWNER_PIDM = @OwnerPidm;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.SZRFA_CHAT_SESSION (SESSION_ID, OWNER_PIDM, SESSION_JSON, UPDATED_AT)
                VALUES (@SessionId, @OwnerPidm, @SessionJson, SYSDATETIMEOFFSET());
            END
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { SessionId = sessionId, OwnerPidm = pidm, SessionJson = sessionJson },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<HumanTaskDto>> GetOpenTasksForRoleAsync(string role, CancellationToken ct)
    {
        const string sql = """
            SELECT
                TASK_ID         AS TaskId,
                REQUEST_ID      AS RequestId,
                RUN_ID          AS WorkflowRunId,
                PORT_REQUEST_ID AS PortRequestId,
                GATE_NAME       AS GateName,
                REQUIRED_ROLE   AS RequiredRole,
                ALLOWED_ACTIONS AS AllowedActionsCsv,
                SUMMARY         AS Summary
            FROM dbo.SZRFA_HUMAN_TASK
            WHERE REQUIRED_ROLE = @Role
              AND STATUS = 'OPEN'
            ORDER BY CREATED_AT ASC
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<TaskRow>(
            new CommandDefinition(sql, new { Role = role }, cancellationToken: ct));

        return rows.Select(r => new HumanTaskDto(
            TaskId: r.TaskId,
            RequestId: r.RequestId,
            WorkflowRunId: r.WorkflowRunId,
            PortRequestId: r.PortRequestId,
            GateName: r.GateName,
            RequiredRole: r.RequiredRole,
            AllowedActions: r.AllowedActionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Summary: r.Summary)).ToList();
    }

    public async Task<HumanTaskDto?> TryClaimTaskAsync(long taskId, string callerRole, long callerPidm, string action, CancellationToken ct)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            const string selectSql = """
                SELECT
                    TASK_ID         AS TaskId,
                    REQUEST_ID      AS RequestId,
                    RUN_ID          AS WorkflowRunId,
                    PORT_REQUEST_ID AS PortRequestId,
                    GATE_NAME       AS GateName,
                    REQUIRED_ROLE   AS RequiredRole,
                    OWNER_PIDM      AS OwnerPidm,
                    ALLOWED_ACTIONS AS AllowedActionsCsv,
                    SUMMARY         AS Summary,
                    STATUS          AS Status
                FROM dbo.SZRFA_HUMAN_TASK WITH (UPDLOCK, ROWLOCK)
                WHERE TASK_ID = @TaskId
                """;

            var row = await conn.QuerySingleOrDefaultAsync<ClaimableTaskRow>(
                new CommandDefinition(selectSql, new { TaskId = taskId }, tx, cancellationToken: ct));

            if (row is null || row.Status != "OPEN")
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            var allowedActions = row.AllowedActionsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            bool authorized = row.RequiredRole == FinAidApproverRoles.Student
                ? callerRole == FinAidApproverRoles.Student && row.OwnerPidm == callerPidm
                : callerRole == row.RequiredRole;

            if (!authorized || !allowedActions.Contains(action, StringComparer.OrdinalIgnoreCase))
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            const string claimSql = """
                UPDATE dbo.SZRFA_HUMAN_TASK
                SET STATUS = 'CLAIMED'
                WHERE TASK_ID = @TaskId AND STATUS = 'OPEN'
                """;

            var updated = await conn.ExecuteAsync(
                new CommandDefinition(claimSql, new { TaskId = taskId }, tx, cancellationToken: ct));

            if (updated != 1)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            await tx.CommitAsync(ct);

            return new HumanTaskDto(
                TaskId: row.TaskId,
                RequestId: row.RequestId,
                WorkflowRunId: row.WorkflowRunId,
                PortRequestId: row.PortRequestId,
                GateName: row.GateName,
                RequiredRole: row.RequiredRole,
                AllowedActions: allowedActions,
                Summary: row.Summary);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task MarkTaskDoneAsync(long taskId, CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.SZRFA_HUMAN_TASK
            SET STATUS = 'DONE', COMPLETED_AT = SYSDATETIMEOFFSET()
            WHERE TASK_ID = @TaskId
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { TaskId = taskId }, cancellationToken: ct));
    }

    public async Task ReleaseClaimedTaskAsync(long taskId, CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.SZRFA_HUMAN_TASK
            SET STATUS = 'OPEN'
            WHERE TASK_ID = @TaskId AND STATUS = 'CLAIMED'
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { TaskId = taskId }, cancellationToken: ct));
    }

    public async Task UpdateStatusAndAuditAsync(
        long requestId,
        RequestStatus newStatus,
        string actorRole,
        string actorId,
        string action,
        string? comment,
        string? notifyRole,
        bool notifyStudent,
        CancellationToken ct)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            const string updateReqSql = """
                UPDATE dbo.STUHU_FIN_AID_REQUESTS
                SET STATUS = @NewStatus, LAST_COMMENT = @CommentText
                WHERE REQUEST_ID = @RequestId
                """;

            await conn.ExecuteAsync(new CommandDefinition(
                updateReqSql,
                new { RequestId = requestId, NewStatus = newStatus.ToString(), CommentText = comment },
                tx,
                cancellationToken: ct));

            const string insertAuditSql = """
                INSERT INTO dbo.SZRFA_AUDIT (
                    REQUEST_ID, ACTOR_ROLE, ACTOR_ID, ACTION, NEW_STATUS, COMMENT_TEXT, CREATED_AT
                ) VALUES (
                    @RequestId, @ActorRole, @ActorId, @Action, @NewStatus, @CommentText, SYSDATETIMEOFFSET()
                )
                """;

            await conn.ExecuteAsync(new CommandDefinition(
                insertAuditSql,
                new
                {
                    RequestId = requestId,
                    ActorRole = actorRole,
                    ActorId = actorId,
                    Action = action,
                    NewStatus = newStatus.ToString(),
                    CommentText = comment
                },
                tx,
                cancellationToken: ct));

            const string insertOutboxSql = """
                IF NOT EXISTS (
                    SELECT 1 FROM dbo.SZRFA_OUTBOX WHERE KIND = @Kind AND DEDUP_KEY = @DedupKey
                )
                BEGIN
                    INSERT INTO dbo.SZRFA_OUTBOX (KIND, DEDUP_KEY, PAYLOAD_JSON, STATUS, ATTEMPTS, CREATED_AT)
                    VALUES (@Kind, @DedupKey, @PayloadJson, 'PENDING', 0, SYSDATETIMEOFFSET());
                END
                """;

            if (!string.IsNullOrWhiteSpace(notifyRole))
            {
                await conn.ExecuteAsync(new CommandDefinition(
                    insertOutboxSql,
                    new
                    {
                        Kind = "NOTIFY_ROLE",
                        DedupKey = $"role-{notifyRole}-{requestId}-{newStatus}",
                        PayloadJson = JsonSerializer.Serialize(new { requestId, role = notifyRole, status = newStatus.ToString() }, JsonOptions)
                    },
                    tx,
                    cancellationToken: ct));
            }

            if (notifyStudent)
            {
                await conn.ExecuteAsync(new CommandDefinition(
                    insertOutboxSql,
                    new
                    {
                        Kind = "NOTIFY_STUDENT",
                        DedupKey = $"student-{requestId}-{newStatus}",
                        PayloadJson = JsonSerializer.Serialize(new { requestId, status = newStatus.ToString() }, JsonOptions)
                    },
                    tx,
                    cancellationToken: ct));
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<string?> LoadCheckpointJsonAsync(string runId, CancellationToken ct)
    {
        const string sql = """
            SELECT PAYLOAD_JSON
            FROM dbo.SZRFA_WF_CHECKPOINT
            WHERE RUN_ID = @RunId
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        return await conn.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, new { RunId = runId }, cancellationToken: ct));
    }

    public async Task SaveCheckpointJsonAsync(string runId, string checkpointId, string? parentId, string payloadJson, CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.SZRFA_WF_CHECKPOINT
            SET CHECKPOINT_ID = @CheckpointId,
                PARENT_ID = @ParentId,
                PAYLOAD_JSON = @PayloadJson,
                UPDATED_AT = SYSDATETIMEOFFSET()
            WHERE RUN_ID = @RunId;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.SZRFA_WF_CHECKPOINT (RUN_ID, CHECKPOINT_ID, PARENT_ID, PAYLOAD_JSON, UPDATED_AT)
                VALUES (@RunId, @CheckpointId, @ParentId, @PayloadJson, SYSDATETIMEOFFSET());
            END
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { RunId = runId, CheckpointId = checkpointId, ParentId = parentId, PayloadJson = payloadJson },
            cancellationToken: ct));
    }

    public async Task CreateHumanTaskAsync(HumanTaskDto task, long ownerPidm, CancellationToken ct)
    {
        const string sql = """
            IF NOT EXISTS (
                SELECT 1
                FROM dbo.SZRFA_HUMAN_TASK
                WHERE RUN_ID = @RunId AND PORT_REQUEST_ID = @PortRequestId
            )
            BEGIN
                INSERT INTO dbo.SZRFA_HUMAN_TASK (
                    REQUEST_ID, RUN_ID, PORT_REQUEST_ID, GATE_NAME,
                    REQUIRED_ROLE, OWNER_PIDM, ALLOWED_ACTIONS, SUMMARY, STATUS, CREATED_AT
                ) VALUES (
                    @RequestId, @RunId, @PortRequestId, @GateName,
                    @RequiredRole, @OwnerPidm, @AllowedActions, @Summary, 'OPEN', SYSDATETIMEOFFSET()
                );
            END
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                task.RequestId,
                RunId = task.WorkflowRunId,
                task.PortRequestId,
                task.GateName,
                task.RequiredRole,
                OwnerPidm = ownerPidm,
                AllowedActions = string.Join(',', task.AllowedActions),
                task.Summary
            },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingOutboxAsync(int batchSize, CancellationToken ct)
    {
        const string sql = """
            ;WITH cte AS (
                SELECT TOP (@BatchSize)
                    OUTBOX_ID, KIND, DEDUP_KEY, PAYLOAD_JSON, ATTEMPTS
                FROM dbo.SZRFA_OUTBOX WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE STATUS = 'PENDING'
                ORDER BY OUTBOX_ID ASC
            )
            UPDATE cte
            SET STATUS = 'PROCESSING',
                ATTEMPTS = ATTEMPTS + 1
            OUTPUT
                INSERTED.OUTBOX_ID   AS OutboxId,
                INSERTED.KIND        AS Kind,
                INSERTED.DEDUP_KEY   AS DedupKey,
                INSERTED.PAYLOAD_JSON AS PayloadJson,
                INSERTED.ATTEMPTS    AS Attempts;
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<OutboxRow>(
            new CommandDefinition(sql, new { BatchSize = batchSize }, cancellationToken: ct));

        return rows.Select(r => new OutboxMessage(r.OutboxId, r.Kind, r.DedupKey, r.PayloadJson, r.Attempts)).ToList();
    }

    public async Task MarkOutboxProcessedAsync(long outboxId, CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.SZRFA_OUTBOX
            SET STATUS = 'PROCESSED', LAST_ERROR = NULL
            WHERE OUTBOX_ID = @OutboxId
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { OutboxId = outboxId }, cancellationToken: ct));
    }

    public async Task MarkOutboxFailedAsync(long outboxId, string error, CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.SZRFA_OUTBOX
            SET STATUS = 'FAILED', LAST_ERROR = @LastError
            WHERE OUTBOX_ID = @OutboxId
            """;

        var trimmedError = error.Length > 1000 ? error[..1000] : error;
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { OutboxId = outboxId, LastError = trimmedError },
            cancellationToken: ct));
    }

    private sealed record RequestRow(long RequestId, string AidTypeRaw, string StatusRaw, DateTimeOffset CreatedAt, string? LastComment);
    private sealed record TaskRow(long TaskId, long RequestId, string WorkflowRunId, string PortRequestId, string GateName, string RequiredRole, string AllowedActionsCsv, string Summary);
    private sealed record ClaimableTaskRow(long TaskId, long RequestId, string WorkflowRunId, string PortRequestId, string GateName, string RequiredRole, long OwnerPidm, string AllowedActionsCsv, string Summary, string Status);
    private sealed record OutboxRow(long OutboxId, string Kind, string DedupKey, string PayloadJson, int Attempts);
}