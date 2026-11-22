using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Notifications;
using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence;
using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Workflows;

namespace HorizonUniversity.AI.FinancialAid.Worker.Outbox;

public sealed class OutboxDispatcherWorker(
    IFinancialAidRepository repository,
    IFinancialAidWorkflowHost workflowHost,
    INotificationSender notificationSender,
    ILogger<OutboxDispatcherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var batch = await repository.ClaimPendingOutboxAsync(batchSize: 20, stoppingToken);
            if (batch.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }

            foreach (var msg in batch)
            {
                try
                {
                    if (msg.Kind == "START_WORKFLOW")
                    {
                        var initial = JsonSerializer.Deserialize<WorkflowCaseState>(msg.PayloadJson)!;
                        await workflowHost.StartWorkflowAsync(initial, stoppingToken);
                    }
                    else
                    {
                        await notificationSender.SendAsync(msg, stoppingToken);
                    }

                    await repository.MarkOutboxProcessedAsync(msg.OutboxId, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Outbox processing failed for ID {OutboxId}", msg.OutboxId);
                    await repository.MarkOutboxFailedAsync(msg.OutboxId, ex.Message, stoppingToken);
                }
            }
        }
    }
}