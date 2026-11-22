using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace HorizonUniversity.AI.FinancialAid.Workflows;

public static class DependencyInjection
{
    public static IServiceCollection AddFinancialAidWorkflows(this IServiceCollection services)
    {
        services.AddScoped<FinancialAidApprovalWorkflowFactory>();
        services.AddScoped<IFinancialAidWorkflowHost, FinancialAidWorkflowHost>();
        return services;
    }
}
