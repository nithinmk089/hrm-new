namespace HRMS.Application.Abstractions.Persistence;

/// <summary>Safe diagnostic metadata for a failed API request.</summary>
public sealed record ApiErrorLogEntry(
    Guid? TenantId,
    Guid? UserId,
    string TraceId,
    string HttpMethod,
    string RequestPath,
    int StatusCode,
    string ErrorType,
    string? ErrorMessage,
    string? StackTrace);
