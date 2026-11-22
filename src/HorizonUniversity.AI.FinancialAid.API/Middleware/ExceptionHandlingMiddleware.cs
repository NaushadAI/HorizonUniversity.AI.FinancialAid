namespace HorizonUniversity.AI.FinancialAid.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context) => next(context);
}
