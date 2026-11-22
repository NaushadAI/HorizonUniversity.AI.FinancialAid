namespace HorizonUniversity.AI.FinancialAid.Domain.Entities;

public sealed class AuditRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
