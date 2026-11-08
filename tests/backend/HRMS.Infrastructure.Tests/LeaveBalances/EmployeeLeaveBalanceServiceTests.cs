using HRMS.Application.LeaveBalances;
using Xunit;

namespace HRMS.Infrastructure.Tests.LeaveBalances;

public sealed class EmployeeLeaveBalanceServiceTests
{
    [Fact]
    public async Task ApplyTransactionAsync_NormalizesTransactionTypeAndNotes()
    {
        var repository = new FakeRepository();
        var service = new EmployeeLeaveBalanceService(repository);
        await service.ApplyTransactionAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new ApplyLeaveBalanceTransactionRequest(Guid.NewGuid(), 2027, " Opening ", 12, " initial grant "));

        Assert.Equal("Opening", repository.Request!.TransactionType);
        Assert.Equal("initial grant", repository.Request.Notes);
    }

    [Fact]
    public async Task ApplyTransactionAsync_RejectsZeroDelta()
    {
        var service = new EmployeeLeaveBalanceService(new FakeRepository());
        await Assert.ThrowsAsync<ArgumentException>(() => service.ApplyTransactionAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new ApplyLeaveBalanceTransactionRequest(Guid.NewGuid(), 2027, "Opening", 0, null)));
    }

    private sealed class FakeRepository : IEmployeeLeaveBalanceRepository
    {
        public ApplyLeaveBalanceTransactionRequest? Request { get; private set; }

        public Task<IReadOnlyList<EmployeeLeaveBalanceDto>> GetAllAsync(Guid tenantId, Guid companyId, Guid employeeId, short leaveYear, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EmployeeLeaveBalanceDto>>([]);

        public Task<EmployeeLeaveBalanceDto> ApplyTransactionAsync(Guid tenantId, Guid companyId, Guid employeeId, Guid actorUserId, ApplyLeaveBalanceTransactionRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new EmployeeLeaveBalanceDto(Guid.NewGuid(), companyId, employeeId, request.LeaveTypeId,
                "AL", "Annual Leave", request.LeaveYear, 0, request.DaysDelta, 0, 0, 0, 0, request.DaysDelta,
                DateTime.UtcNow, null, new byte[8]));
        }
    }
}
