namespace HorizonUniversity.AI.FinancialAid.Domain.Entities;

public sealed class FinancialAidRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
