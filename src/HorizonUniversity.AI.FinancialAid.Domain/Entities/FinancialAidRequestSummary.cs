using HorizonUniversity.AI.FinancialAid.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Domain.Entities
{
    public sealed record FinancialAidRequestSummary(
    long RequestId,
    AidType AidType,
    RequestStatus Status,
    DateTimeOffset CreatedAt,
    string? LastComment);
}
