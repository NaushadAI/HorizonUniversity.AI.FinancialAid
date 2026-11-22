using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Domain.Entities
{
    public sealed record HumanTaskDto(
    long TaskId,
    long RequestId,
    string WorkflowRunId,
    string PortRequestId,
    string GateName,
    string RequiredRole,
    IReadOnlyList<string> AllowedActions,
    string Summary);
}
