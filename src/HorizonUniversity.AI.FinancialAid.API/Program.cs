using Azure.AI.OpenAI;
using Azure.Identity;
using HorizonUniversity.AI.FinancialAid.Agents;
using HorizonUniversity.AI.FinancialAid.Application;
using HorizonUniversity.AI.FinancialAid.Domain.Entities;
using HorizonUniversity.AI.FinancialAid.Infrastructure;
using HorizonUniversity.AI.FinancialAid.Workflows;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StudentOnly", p => p.RequireRole(FinAidApproverRoles.Student));
    options.AddPolicy("StaffApprover", p => p.RequireRole(
        FinAidApproverRoles.Cslo, FinAidApproverRoles.FacSecretary, FinAidApproverRoles.Finance, FinAidApproverRoles.Inventory));
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("StudentChatLimit", context =>
    {
        var pidm = context.User.FindFirstValue("pidm") ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(pidm, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1)
        });
    });
});

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var endpoint = new Uri(builder.Configuration["Foundry:Endpoint"]!);
    var deployment = builder.Configuration["Foundry:ChatDeployment"]!;
    return (IChatClient)new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
        .GetChatClient(deployment);
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource("Microsoft.Agents.AI*"));

builder.Services
    .AddFinancialAidApplication()
    .AddFinancialAidAgents()
    .AddFinancialAidWorkflows()
    .AddFinancialAidInfrastructure(builder.Configuration);

//builder.Services.AddFinancialAidInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();