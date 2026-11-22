using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Application.DTOs
{
    public sealed record ChatRequestDto(string? SessionId, string Message);
    public sealed record ChatResponseDto(string SessionId, string Reply);
    public sealed record CompleteTaskCommand(string Action, string? Comment);
}
