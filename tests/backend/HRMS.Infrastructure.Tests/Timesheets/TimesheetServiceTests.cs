using HRMS.Application.Timesheets;
using Xunit;

namespace HRMS.Infrastructure.Tests.Timesheets;

public sealed class TimesheetServiceTests
{
    [Fact]
    public async Task CreateAsync_RequiresMondayWeekStart()
    {
        var service = new TimesheetService(new FakeRepository(), TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new TimesheetCreateRequest(new DateOnly(2026, 11, 8))));
    }

    [Fact]
    public async Task SaveEntriesAsync_RequiresEightByteRowVersion()
    {
        var service = new TimesheetService(new FakeRepository(), TimeProvider.System);
        var request = new SaveTimesheetEntriesRequest([1, 2], [Entry()]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveEntriesAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request));
    }

    [Fact]
    public async Task SaveEntriesAsync_RejectsUnsupportedCategory()
    {
        var service = new TimesheetService(new FakeRepository(), TimeProvider.System);
        var request = new SaveTimesheetEntriesRequest(new byte[8],
            [Entry(category: "Unplanned")]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveEntriesAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request));
    }

    [Fact]
    public async Task SaveEntriesAsync_RejectsDailyTotalAbove24Hours()
    {
        var service = new TimesheetService(new FakeRepository(), TimeProvider.System);
        var date = new DateOnly(2026, 11, 2);
        var request = new SaveTimesheetEntriesRequest(new byte[8],
            [Entry(date, hours: 16), Entry(date, hours: 8.25m)]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveEntriesAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request));
    }

    [Fact]
    public async Task SaveEntriesAsync_TrimsEntryTextAndPassesRowVersion()
    {
        var repository = new FakeRepository();
        var service = new TimesheetService(repository, TimeProvider.System);
        var rowVersion = new byte[8];
        var request = new SaveTimesheetEntriesRequest(rowVersion,
            [Entry(description: "  Reviewed support tickets  ")]);

        await service.SaveEntriesAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.Equal("Reviewed support tickets", repository.SavedRequest?.Entries[0].Description);
        Assert.Equal(rowVersion, repository.SavedRequest?.RowVersion);
    }

    [Fact]
    public async Task RejectAsync_RequiresComments()
    {
        var service = new TimesheetService(new FakeRepository(), TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => service.RejectAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new TimesheetDecisionRequest(new byte[8], "  ")));
    }

    private static TimesheetEntryInput Entry(
        DateOnly? date = null, string category = "ClientWork",
        string description = "Implementation work", decimal hours = 1m) =>
        new(date ?? new DateOnly(2026, 11, 2), category, description, hours);

    private sealed class FakeRepository : ITimesheetRepository
    {
        public SaveTimesheetEntriesRequest? SavedRequest { get; private set; }

        public Task<TimesheetDetailDto?> GetMineAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, DateOnly weekStartDate,
            CancellationToken cancellationToken) => Task.FromResult<TimesheetDetailDto?>(null);

        public Task<TimesheetHistoryResponse> GetMineHistoryAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken) =>
            Task.FromResult(new TimesheetHistoryResponse([], []));

        public Task<TimesheetCreateResult> CreateAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, DateOnly weekStartDate,
            CancellationToken cancellationToken) =>
            Task.FromResult(new TimesheetCreateResult(Guid.NewGuid(), weekStartDate,
                weekStartDate.AddDays(6), "Draft", new byte[8]));

        public Task<SaveTimesheetEntriesResult> SaveEntriesAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
            SaveTimesheetEntriesRequest request, CancellationToken cancellationToken)
        {
            SavedRequest = request;
            return Task.FromResult(new SaveTimesheetEntriesResult(timesheetId, "Draft", new byte[8], 1m));
        }

        public Task<TimesheetSubmitResult> SubmitAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
            byte[] rowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(new TimesheetSubmitResult(timesheetId, "Submitted", DateTime.UtcNow, new byte[8]));

        public Task<IReadOnlyList<TimesheetApprovalQueueItem>> GetPendingApprovalsAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TimesheetApprovalQueueItem>>([]);

        public Task<TimesheetDecisionResult> DecideAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
            string decision, TimesheetDecisionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new TimesheetDecisionResult(
                timesheetId, decision, Guid.NewGuid(), DateTime.UtcNow, new byte[8]));
    }
}
