namespace HorizonUniversity.AI.FinancialAid.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context) => next(context);
}
