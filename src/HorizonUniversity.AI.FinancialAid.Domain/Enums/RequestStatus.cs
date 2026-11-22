using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Domain.Enums
{
    
    public enum RequestStatus
    {
        UnderReview,
        RevertedToStudent,
        PendingAssessment,
        Rejected,
        ApprovedAwaitingFinanceTeam,
        ApprovedAwaitingInventoryTeam,
        ApprovedAwaitingExecution,
        ApprovedInTransition,
        ReadyForCollection,
        Disbursed,
        CashAidReceived,
        LaptopReceived,
        AssistanceOthersReceived
    }
}
