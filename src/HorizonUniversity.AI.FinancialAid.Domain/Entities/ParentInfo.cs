using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Domain.Entities
{
    public sealed record ParentInfo(
    decimal MonthlySalary,
    string? Nationality = null,
    string? Workplace = null,
    string? JobRole = null);
}
