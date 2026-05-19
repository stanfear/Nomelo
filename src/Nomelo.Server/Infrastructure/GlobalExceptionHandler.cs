using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Nomelo.Server.Infrastructure;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var env = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        var (status, title) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception on {Path}", httpContext.Request.Path);
        else
            logger.LogWarning(exception, "Client error on {Path}", httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = env.IsDevelopment() || status < 500 ? exception.Message : null,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = problem.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
