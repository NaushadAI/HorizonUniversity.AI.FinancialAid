using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Agents;
using HorizonUniversity.AI.FinancialAid.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HorizonUniversity.AI.FinancialAid.API.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize(Policy = "StudentOnly")]
public sealed class ChatController(ChatService chatService) : BaseApiController
{
    [HttpPost]
    [EnableRateLimiting("StudentChatLimit")]
    public async Task<ActionResult<ChatResponseDto>> Post([FromBody] ChatRequestDto req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Message) || req.Message.Length > 2000)
            return BadRequest(new { error = "Message must be between 1 and 2000 characters." });

        var pidm = GetPidm();
        var sessionId = string.IsNullOrWhiteSpace(req.SessionId) ? Guid.NewGuid().ToString("N") : req.SessionId;
        var reply = await chatService.SendAsync(pidm, sessionId, req.Message, ct);

        return Ok(new ChatResponseDto(sessionId, reply));
    }
}
