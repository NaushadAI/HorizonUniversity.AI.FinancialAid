using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence;
using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Application.Abstractions.Agents
{
    public sealed class ChatService(IFinancialAidIntakeAgent intakeAgent)
    {
        public async Task<string> SendAsync(long pidm, string sessionId, string message, CancellationToken ct)
            => await intakeAgent.HandleMessageAsync(pidm, sessionId, message, ct);
    }
}
