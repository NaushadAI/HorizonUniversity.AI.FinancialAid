using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence
{
    public interface IFinancialAidIntakeAgent
    {
        Task<string> HandleMessageAsync(long pidm, string sessionId, string userMessage, CancellationToken ct);
    }
}
