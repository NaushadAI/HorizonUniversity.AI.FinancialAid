using HorizonUniversity.AI.FinancialAid.Application.Abstractions.Persistence;
using HorizonUniversity.AI.FinancialAid.Infrastructure.SqlServer.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HorizonUniversity.AI.FinancialAid.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFinancialAidInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Wire ODP.NET / Dapper OracleFinancialAidRepository & OutboxNotificationSender here
        services.AddScoped<IFinancialAidRepository, SqlServerFinancialAidRepository>();
        return services;
    }
}