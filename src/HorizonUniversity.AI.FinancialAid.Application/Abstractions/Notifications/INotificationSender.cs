using HorizonUniversity.AI.FinancialAid.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Application.Abstractions.Notifications
{
    public interface INotificationSender
    {
        Task SendAsync(OutboxMessage message, CancellationToken ct);
    }
}
