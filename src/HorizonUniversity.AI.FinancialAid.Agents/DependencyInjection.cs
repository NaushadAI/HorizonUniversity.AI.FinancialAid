using HorizonUniversity.AI.FinancialAid.Agents.Intake;
using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace HorizonUniversity.AI.FinancialAid.Agents;

public static class DependencyInjection
{
    public static IServiceCollection AddFinancialAidAgents(this IServiceCollection services)
    {
        services.AddScoped<IFinancialAidIntakeAgent, FinancialAidIntakeAgent>();
        return services;
    }
}