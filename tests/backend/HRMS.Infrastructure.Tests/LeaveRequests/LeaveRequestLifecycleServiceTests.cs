using HRMS.Application.LeaveRequests;
using Xunit;

namespace HRMS.Infrastructure.Tests.LeaveRequests;

public sealed class LeaveRequestLifecycleServiceTests
{
    [Fact]
    public async Task CancelAsync_RequiresEightByteRowVersion()
    {
        var service = new LeaveRequestLifecycleService(new FakeRepository());
        await Assert.ThrowsAsync<ArgumentException>(() => service.CancelAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new LeaveRequestCancellationRequest([1, 2], null)));
    }

    [Fact]
    public async Task CancelAsync_TrimsCommentsAndPassesRowVersion()
    {
        var repository = new FakeRepository();
        var service = new LeaveRequestLifecycleService(repository);
        var version = new byte[8];

        await service.CancelAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new LeaveRequestCancellationRequest(version, "  Changed plans  "));

        Assert.Equal("Changed plans", repository.Comments);
        Assert.Equal(version, repository.RowVersion);
    }

    [Fact]
    public async Task CancelAsync_RejectsCommentsLongerThanOneThousandCharacters()
    {
        var service = new LeaveRequestLifecycleService(new FakeRepository());
        await Assert.ThrowsAsync<ArgumentException>(() => service.CancelAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new LeaveRequestCancellationRequest(new byte[8], new string('x', 1001))));
    }

    private sealed class FakeRepository : ILeaveRequestLifecycleRepository
    {
        public string? Comments { get; private set; }
        public byte[]? RowVersion { get; private set; }

        public Task<IReadOnlyList<EmployeeLeaveRequestItem>> GetMineAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EmployeeLeaveRequestItem>>([]);

        public Task<IReadOnlyList<LeaveRequestHistoryItem>> GetHistoryAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LeaveRequestHistoryItem>>([]);

        public Task<LeaveRequestCancellationResult> CancelAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
            byte[] rowVersion, string? comments, CancellationToken cancellationToken)
        {
            RowVersion = rowVersion;
            Comments = comments;
            return Task.FromResult(new LeaveRequestCancellationResult(
                leaveRequestId, "Cancelled", DateTime.UtcNow, new byte[8]));
        }
    }
}
