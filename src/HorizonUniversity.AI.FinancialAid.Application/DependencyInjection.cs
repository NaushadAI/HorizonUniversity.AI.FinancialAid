using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Agents;
using HorizonUniversity.AI.FinancialAid.Application.Features.Requests;
using HorizonUniversity.AI.FinancialAid.Application.Features.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace HorizonUniversity.AI.FinancialAid.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddFinancialAidApplication(this IServiceCollection services)
    {
        services.AddScoped<ChatService>();
        services.AddScoped<TasksService>();
        services.AddScoped<RequestsService>();
        return services;
    }
}
