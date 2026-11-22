using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence;
using HorizonUniversity.AI.FinancialAid.Domain.Entities;

namespace HorizonUniversity.AI.FinancialAid.Application.Features.Requests;

public sealed class RequestsService(IFinancialAidRepository repository)
{
    public async Task<FinancialAidRequestSummary?> GetStatusForStudentAsync(long requestId, long pidm, CancellationToken ct)
    {
        var list = await repository.GetRequestsByPidmAsync(pidm, ct);
        return list.FirstOrDefault(r => r.RequestId == requestId);
    }
}