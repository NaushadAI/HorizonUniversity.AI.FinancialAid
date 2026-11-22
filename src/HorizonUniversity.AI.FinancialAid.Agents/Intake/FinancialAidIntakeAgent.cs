using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence;
using HorizonUniversity.AI.FinancialAid.Domain.Entities;
using HorizonUniversity.AI.FinancialAid.Domain.Enums;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HorizonUniversity.AI.FinancialAid.Agents.Intake;

public sealed class FinancialAidIntakeAgent(
    IChatClient foundryChatClient,
    IFinancialAidRepository repository) : IFinancialAidIntakeAgent
{
    private sealed class TurnState
    {
        public required long Pidm { get; init; }
        public required string SessionId { get; init; }
        public bool CanSubmitNow { get; set; }
    }

    public async Task<string> HandleMessageAsync(
        long pidm,
        string sessionId,
        string userMessage,
        CancellationToken ct)
    {
        var existingDraft = await repository.GetPreparedDraftAsync(sessionId, pidm, ct);
        var state = new TurnState
        {
            Pidm = pidm,
            SessionId = sessionId,
            CanSubmitNow = existingDraft is not null
        };

        var tools = new AITool[]
        {
            AIFunctionFactory.Create(
                ([Description("Cash, Laptop, or Other")] string aidType,
                 string? otherDescription,
                 int familyMembers,
                 bool siblingsAtHu,
                 bool moceRecipient,
                 decimal fatherSalary,
                 decimal motherSalary,
                 string justification,
                 string? fatherNationality = null,
                 string? fatherWorkplace = null,
                 string? fatherJobRole = null,
                 string? motherNationality = null,
                 string? motherWorkplace = null,
                 string? motherJobRole = null) =>
                    PrepareRequestAsync(
                        state, aidType, otherDescription, familyMembers, siblingsAtHu,
                        moceRecipient, fatherSalary, motherSalary, justification,
                        fatherNationality, fatherWorkplace, fatherJobRole,
                        motherNationality, motherWorkplace, motherJobRole, ct),
                name: "prepare_request",
                description: "Validate collected financial aid details and save a server-side draft before confirmation."),

            AIFunctionFactory.Create(
                () => SubmitRequestAsync(state, ct),
                name: "submit_request",
                description: "Submit the previously prepared draft after the student explicitly confirms the summary."),

            AIFunctionFactory.Create(
                () => GetMyRequestsAsync(state, ct),
                name: "get_my_requests",
                description: "List the signed-in student's financial aid requests and current statuses.")
        };

        var agent = new ChatClientAgent(
            foundryChatClient,
            new ChatClientAgentOptions
            {
                Name = "HU-FinancialAid-IntakeAgent",
                ChatOptions = new ChatOptions
                {
                    Instructions = IntakeAgentInstructions.SystemPrompt,
                    Temperature = 0.1f,
                    MaxOutputTokens = 800,
                    Tools = tools
                }
            });

        var serializedSession = await repository.LoadChatSessionJsonAsync(sessionId, pidm, ct);
        AgentSession session = serializedSession is not null
            ? await agent.DeserializeSessionAsync(JsonDocument.Parse(serializedSession).RootElement, cancellationToken: ct)
            : await agent.CreateSessionAsync(cancellationToken: ct);

        var response = await agent.RunAsync(userMessage, session, cancellationToken: ct);

        var updatedSession = (await agent.SerializeSessionAsync(session, cancellationToken: ct)).GetRawText();
        await repository.SaveChatSessionJsonAsync(sessionId, pidm, updatedSession, ct);

        return response.ToString();
    }

    private async Task<object> PrepareRequestAsync(
        TurnState state,
        string aidTypeRaw,
        string? otherDescription,
        int familyMembers,
        bool siblingsAtHu,
        bool moceRecipient,
        decimal fatherSalary,
        decimal motherSalary,
        string justification,
        string? fatherNationality,
        string? fatherWorkplace,
        string? fatherJobRole,
        string? motherNationality,
        string? motherWorkplace,
        string? motherJobRole,
        CancellationToken ct)
    {
        if (!Enum.TryParse<AidType>(aidTypeRaw, ignoreCase: true, out var aidType))
            return new { ok = false, errors = new[] { "Aid type must be Cash, Laptop, or Other." } };

        if (await repository.HasOpenRequestAsync(state.Pidm, ct))
            return new { ok = false, errors = new[] { "You already have an active financial aid request. Ask to check its status instead." } };

        var draft = new FinancialAidDraft(
            Pidm: state.Pidm,
            AidType: aidType,
            OtherAidDescription: otherDescription,
            FamilyMembersCount: familyMembers,
            HasSiblingsAtHu: siblingsAtHu,
            IsMoceRecipient: moceRecipient,
            Father: new ParentInfo(fatherSalary, fatherNationality, fatherWorkplace, fatherJobRole),
            Mother: new ParentInfo(motherSalary, motherNationality, motherWorkplace, motherJobRole),
            Justification: justification);

        var validationErrors = FinancialAidValidator.Validate(draft);
        if (validationErrors.Count > 0)
            return new { ok = false, errors = validationErrors };

        await repository.SaveDraftAsync(state.SessionId, state.Pidm, draft, ct);
        state.CanSubmitNow = false;

        return new { ok = true, message = "Draft validated and saved. Present the summary and ask for confirmation." };
    }

    private async Task<object> SubmitRequestAsync(TurnState state, CancellationToken ct)
    {
        if (!state.CanSubmitNow)
            return new { ok = false, error = "Cannot submit until the student confirms the prepared summary in a follow-up message." };

        var requestId = await repository.SubmitDraftAndEnqueueWorkflowAsync(state.SessionId, state.Pidm, ct);
        return new
        {
            ok = true,
            requestId,
            status = nameof(RequestStatus.UnderReview),
            message = $"Request #{requestId} submitted and is now Under Review by CSLO."
        };
    }

    private async Task<object> GetMyRequestsAsync(TurnState state, CancellationToken ct)
    {
        var requests = await repository.GetRequestsByPidmAsync(state.Pidm, ct);
        return new { ok = true, requests };
    }
}