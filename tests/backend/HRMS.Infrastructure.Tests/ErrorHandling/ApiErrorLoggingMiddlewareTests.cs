using System.Security.Claims;
using HRMS.API.ErrorHandling;
using HRMS.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HRMS.Infrastructure.Tests.ErrorHandling;

public sealed class ApiErrorLoggingMiddlewareTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status403Forbidden)]
    [InlineData(StatusCodes.Status409Conflict)]
    [InlineData(StatusCodes.Status422UnprocessableEntity)]
    [InlineData(StatusCodes.Status429TooManyRequests)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    public async Task InvokeAsync_LogsFailureResponsesExcept401And404(int statusCode)
    {
        var repository = new FakeApiErrorLogRepository();
        var context = CreateContext();
        var middleware = CreateMiddleware(statusCode);

        await middleware.InvokeAsync(context, repository, CreateEnvironment());

        var entry = Assert.Single(repository.Entries);
        Assert.Equal(statusCode, entry.StatusCode);
        Assert.Equal("/api/v1/test", entry.RequestPath);
        Assert.Equal("POST", entry.HttpMethod);
        Assert.Equal(context.TraceIdentifier, entry.TraceId);
        Assert.Equal("HRMS.API", entry.ApplicationName);
        Assert.Equal("Development", entry.EnvironmentName);
    }

    [Theory]
    [InlineData(StatusCodes.Status200OK)]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status302Found)]
    public async Task InvokeAsync_DoesNotLogSuccessOrExcludedResponses(int statusCode)
    {
        var repository = new FakeApiErrorLogRepository();
        var context = CreateContext();
        var middleware = CreateMiddleware(statusCode);

        await middleware.InvokeAsync(context, repository, CreateEnvironment());

        Assert.Empty(repository.Entries);
    }

    [Fact]
    public async Task InvokeAsync_IncludesAuthenticatedTenantUserAndExceptionDetails()
    {
        var repository = new FakeApiErrorLogRepository();
        var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var exception = new InvalidOperationException("Concurrency conflict.");

        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("sub", userId.ToString())
        ], "Test"));
        context.Items[GlobalExceptionHandler.ExceptionItemKey] = exception;

        var middleware = CreateMiddleware(StatusCodes.Status409Conflict);

        await middleware.InvokeAsync(context, repository, CreateEnvironment());

        var entry = Assert.Single(repository.Entries);
        Assert.Equal(tenantId, entry.TenantId);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(typeof(InvalidOperationException).FullName, entry.ErrorType);
        Assert.Equal("Concurrency conflict.", entry.ErrorMessage);
        Assert.Contains(nameof(InvalidOperationException), entry.StackTrace);
        Assert.Equal("HRMS.API", entry.ApplicationName);
        Assert.Equal("Development", entry.EnvironmentName);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotThrowWhenErrorLogPersistenceFails()
    {
        var repository = new FakeApiErrorLogRepository
        {
            ExceptionToThrow = new InvalidOperationException("Database unavailable.")
        };
        var context = CreateContext();
        var middleware = CreateMiddleware(StatusCodes.Status500InternalServerError);

        await middleware.InvokeAsync(context, repository, CreateEnvironment());

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    private static ApiErrorLoggingMiddleware CreateMiddleware(int statusCode) =>
        new(
            httpContext =>
            {
                httpContext.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            },
            NullLogger<ApiErrorLoggingMiddleware>.Instance);

    private static IHostEnvironment CreateEnvironment() =>
        new TestHostEnvironment
        {
            ApplicationName = "HRMS.API",
            EnvironmentName = "Development",
            ContentRootPath = AppContext.BaseDirectory,
            ContentRootFileProvider = new NullFileProvider()
        };

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/test";
        context.TraceIdentifier = "test-trace-id";
        return context;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HRMS.API";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeApiErrorLogRepository : IApiErrorLogRepository
    {
        public List<ApiErrorLogEntry> Entries { get; } = [];
        public Exception? ExceptionToThrow { get; init; }

        public Task InsertAsync(
            ApiErrorLogEntry entry,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
