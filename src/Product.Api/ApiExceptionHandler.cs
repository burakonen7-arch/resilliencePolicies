using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;
using Product.Infrastructure;
namespace Product.Api;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        logger.LogWarning(exception, "Request failed: {ExceptionType}", exception.GetType().Name);
        var (status, title) = exception switch
        {
            BrokenCircuitException => (503, "Circuit is open; retry after 5 seconds"),
            RateLimiterRejectedException => (429, "Rate or concurrency limit exceeded"),
            TimeoutRejectedException => (504, "Operation timed out"),
            SimulatedFailureException => (503, "Simulated transient failure"),
            ArgumentException => (400, "Unsupported policy"),
            _ => (500, "An unexpected error occurred")
        };
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = new ProblemDetails { Status = status, Title = title } });
    }
}
