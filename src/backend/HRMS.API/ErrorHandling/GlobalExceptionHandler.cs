using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.ErrorHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const string ExceptionItemKey = "HRMS.Exception";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        httpContext.Items[ExceptionItemKey] = exception;

        var (status, title, detail) = exception switch
        {
            BadHttpRequestException badRequest =>
                (badRequest.StatusCode, "Invalid request.", badRequest.Message),
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "Invalid request.", exception.Message),
            InvalidOperationException =>
                (StatusCodes.Status409Conflict,
                    "The requested operation conflicts with the current state.", exception.Message),
            _ =>
                (StatusCodes.Status500InternalServerError, "An unexpected error occurred.",
                    "The server encountered an unexpected error. Please try again later.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception,
                "An unhandled exception occurred while processing {Method} {Path}.",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(exception,
                "A request error occurred while processing {Method} {Path} with status {StatusCode}.",
                httpContext.Request.Method, httpContext.Request.Path, status);
        }

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
