using System.IdentityModel.Tokens.Jwt;
using HRMS.Application.Abstractions.Persistence;

namespace HRMS.API.ErrorHandling;

/// <summary>
/// Persists failed HTTP responses except authentication failures (401) and
/// missing-resource responses (404). Runs outside exception handling so it
/// observes the final status code produced by the exception handler.
/// </summary>
public sealed class ApiErrorLoggingMiddleware(
    RequestDelegate next,
    ILogger<ApiErrorLoggingMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IApiErrorLogRepository repository)
    {
        await next(context);

        var statusCode = context.Response.StatusCode;
        if (statusCode < StatusCodes.Status400BadRequest ||
            statusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status404NotFound)
        {
            return;
        }

        var exception = context.Items.TryGetValue(
            GlobalExceptionHandler.ExceptionItemKey, out var value)
            ? value as Exception
            : null;

        var entry = new ApiErrorLogEntry(
            ParseGuid(context.User.FindFirst("tenant_id")?.Value),
            ParseGuid(context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value),
            context.TraceIdentifier,
            Limit(context.Request.Method, 10),
            Limit(context.Request.Path.Value ?? "/", 2048),
            statusCode,
            Limit(exception?.GetType().FullName ?? "HttpFailure", 500),
            exception?.Message ?? ((System.Net.HttpStatusCode)statusCode).ToString(),
            exception?.StackTrace);

        try
        {
            // Do not use RequestAborted: the client may disconnect after the
            // server has already produced the failure response.
            await repository.InsertAsync(entry, CancellationToken.None);
        }
        catch (Exception loggingException)
        {
            // Error logging must never replace or hide the original API response.
            logger.LogError(loggingException,
                "Failed to persist API error log for trace {TraceId}.",
                context.TraceIdentifier);
        }
    }

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
