using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Domain.Entities
{
    public sealed record OutboxMessage(
    long OutboxId,
    string Kind,
    string DedupKey,
    string PayloadJson,
    int Attempts);
}
