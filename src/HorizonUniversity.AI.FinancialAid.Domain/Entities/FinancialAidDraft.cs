using HorizonUniversity.AI.FinancialAid.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Domain.Entities
{
    public sealed record FinancialAidDraft(
    long Pidm,
    AidType AidType,
    string? OtherAidDescription,
    int FamilyMembersCount,
    bool HasSiblingsAtHu,
    bool IsMoceRecipient,
    ParentInfo Father,
    ParentInfo Mother,
    string Justification,
    long? ExistingRequestId = null);
}
