namespace HorizonUniversity.AI.FinancialAid.Domain.Entities;

public sealed class WorkflowCheckpoint
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
