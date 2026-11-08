using HRMS.Application.LeaveRequests;
using Xunit;

namespace HRMS.Infrastructure.Tests.LeaveRequests;

public sealed class LeaveApprovalServiceTests
{
    [Fact]
    public async Task ApproveAsync_RequiresEightByteRowVersion()
    {
        var service = new LeaveApprovalService(new FakeRepository());
        await Assert.ThrowsAsync<ArgumentException>(() => service.ApproveAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new LeaveApprovalDecisionRequest([1, 2, 3], null)));
    }

    [Fact]
    public async Task RejectAsync_RequiresComments()
    {
        var service = new LeaveApprovalService(new FakeRepository());
        await Assert.ThrowsAsync<ArgumentException>(() => service.RejectAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new LeaveApprovalDecisionRequest(new byte[8], "  ")));
    }

    [Fact]
    public async Task RejectAsync_TrimsCommentsAndPassesExpectedAction()
    {
        var repository = new FakeRepository();
        var service = new LeaveApprovalService(repository);
        var rowVersion = new byte[8];

        await service.RejectAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new LeaveApprovalDecisionRequest(rowVersion, "  Not enough information  "));

        Assert.Equal("Rejected", repository.Action);
        Assert.Equal("Not enough information", repository.Comments);
        Assert.Equal(rowVersion, repository.RowVersion);
    }

    private sealed class FakeRepository : ILeaveApprovalRepository
    {
        public string? Action { get; private set; }
        public string? Comments { get; private set; }
        public byte[]? RowVersion { get; private set; }

        public Task<IReadOnlyList<LeaveApprovalQueueItem>> GetPendingAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LeaveApprovalQueueItem>>([]);

        public Task<LeaveApprovalDecisionResult> DecideAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
            string action, byte[] rowVersion, string? comments, CancellationToken cancellationToken)
        {
            Action = action;
            RowVersion = rowVersion;
            Comments = comments;
            return Task.FromResult(new LeaveApprovalDecisionResult(
                leaveRequestId, action, Guid.NewGuid(), DateTime.UtcNow, new byte[8]));
        }
    }
}
