using HorizonUniversity.AI.FinancialAid.Application.DTOs;
using HorizonUniversity.AI.FinancialAid.Application.Features.Tasks;
using HorizonUniversity.AI.FinancialAid.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HorizonUniversity.AI.FinancialAid.API.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public sealed class TasksController(TasksService tasksService) : BaseApiController
{
    [HttpGet]
    [Authorize(Policy = "StaffApprover")]
    public async Task<IActionResult> Get([FromQuery] string role, CancellationToken ct)
    {
        if (!User.IsInRole(role))
            return Forbid();

        var tasks = await tasksService.GetOpenTasksAsync(role, ct);
        return Ok(tasks);
    }

    [HttpPost("{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, [FromBody] CompleteTaskCommand cmd, CancellationToken ct)
    {
        var pidm = GetPidm();
        var role = User.FindAll(ClaimTypes.Role).Select(c => c.Value).FirstOrDefault() ?? FinAidApproverRoles.Student;
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? pidm.ToString();

        var (claimed, error) = await tasksService.CompleteTaskAsync(id, cmd, role, pidm, actorId, ct);
        return claimed
            ? Ok(new { taskId = id, status = "DONE" })
            : Conflict(new { error });
    }
}
