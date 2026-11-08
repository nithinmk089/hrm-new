using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace HRMS.API.ErrorHandling;
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An unhandled exception occurred while processing {Method} {Path}.",
            httpContext.Request.Method, httpContext.Request.Path);
        var (status, title, detail) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request.", exception.Message),
            InvalidOperationException => (StatusCodes.Status409Conflict,
                "The requested operation conflicts with the current state.", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.",
                "The server encountered an unexpected error. Please try again later.")
        };
        var problemDetails = new ProblemDetails {
            Status = status, Title = title, Detail = detail, Instance = httpContext.Request.Path
        };
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}