using Azure.AI.OpenAI;
using Azure.Identity;
using HorizonUniversity.AI.FinancialAid.Agents;
using HorizonUniversity.AI.FinancialAid.Application;
using HorizonUniversity.AI.FinancialAid.Worker.Outbox;
using HorizonUniversity.AI.FinancialAid.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = new Uri(config["Foundry:Endpoint"]!);
    var deployment = config["Foundry:ChatDeployment"]!;
    return (IChatClient)new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
        .GetChatClient(deployment);
});

builder.Services
    .AddFinancialAidApplication()
    .AddFinancialAidAgents()
    .AddFinancialAidWorkflows();

// Register your Oracle repository & notification sender in Infrastructure.DependencyInjection:
// builder.Services.AddFinancialAidInfrastructure(builder.Configuration);

builder.Services.AddHostedService<OutboxDispatcherWorker>();

var host = builder.Build();
host.Run();