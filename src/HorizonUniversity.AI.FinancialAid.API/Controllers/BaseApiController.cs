using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Agents;
using HorizonUniversity.AI.FinancialAid.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace HorizonUniversity.AI.FinancialAid.API.Controllers
{
    [ApiController]
    [Route("api")]
    public abstract class BaseApiController : ControllerBase
    {
        protected long GetPidm() =>
            long.TryParse(User.FindFirstValue("pidm"), out var pidm)
                ? pidm
                : throw new UnauthorizedAccessException("Missing or invalid PIDM claim.");
    }
}
