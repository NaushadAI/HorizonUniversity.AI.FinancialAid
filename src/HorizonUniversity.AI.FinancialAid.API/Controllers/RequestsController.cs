using HorizonUniversity.AI.FinancialAid.Application.Features.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorizonUniversity.AI.FinancialAid.API.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize(Policy = "StudentOnly")]
public sealed class RequestsController(RequestsService requestsService) : BaseApiController
{
    [HttpGet("{id:long}/status")]
    public async Task<IActionResult> GetStatus(long id, CancellationToken ct)
    {
        var pidm = GetPidm();
        var summary = await requestsService.GetStatusForStudentAsync(id, pidm, ct);
        return summary is null ? NotFound() : Ok(summary);
    }
}
