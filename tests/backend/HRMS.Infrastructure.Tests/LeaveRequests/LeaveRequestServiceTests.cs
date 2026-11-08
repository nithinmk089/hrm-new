using HRMS.Application.LeaveRequests;
using Xunit;

namespace HRMS.Infrastructure.Tests.LeaveRequests;

public sealed class LeaveRequestServiceTests
{
    [Fact]
    public async Task SubmitAsync_CalculatesChargeableDaysAndSplitsAcrossCalendarYears()
    {
        var employeeId = Guid.NewGuid();
        var leaveTypeId = Guid.NewGuid();
        var repository = new FakeRepository(new LeaveRequestCalculationContext(
            employeeId, leaveTypeId, "ANNUAL", "Annual Leave", true, true,
            MondayToFriday,
            new Dictionary<Guid, IReadOnlyList<LeaveWorkingWeekDay>>(),
            [],
            []));
        var service = new LeaveRequestService(repository);

        var result = await service.SubmitAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new SubmitLeaveRequest(
                leaveTypeId,
                new DateOnly(2026, 12, 31),
                new DateOnly(2027, 1, 4),
                false,
                null,
                "Year-end leave"));

        Assert.Equal(3m, repository.DaysRequested);
        Assert.Equal(
            new[] { new LeaveYearReservationDto(2026, 1m), new LeaveYearReservationDto(2027, 2m) },
            repository.Allocations);
        Assert.Equal(3m, result.Request.DaysRequested);
    }

    [Fact]
    public async Task SubmitAsync_RejectsZeroChargeableDays()
    {
        var leaveTypeId = Guid.NewGuid();
        var companyWeek = Enumerable.Range(1, 7)
            .Select(day => new LeaveWorkingWeekDay((byte)day, false))
            .ToArray();
        var repository = new FakeRepository(new LeaveRequestCalculationContext(
            Guid.NewGuid(), leaveTypeId, "ANNUAL", "Annual Leave", true, false,
            companyWeek,
            new Dictionary<Guid, IReadOnlyList<LeaveWorkingWeekDay>>(),
            [],
            []));
        var service = new LeaveRequestService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new SubmitLeaveRequest(leaveTypeId,
                new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 8),
                false, null, "Weekly off")));
        Assert.Null(repository.Allocations);
    }

    [Fact]
    public async Task SubmitAsync_NormalizesReasonAndHalfDayPeriod()
    {
        var leaveTypeId = Guid.NewGuid();
        var repository = new FakeRepository(new LeaveRequestCalculationContext(
            Guid.NewGuid(), leaveTypeId, "ANNUAL", "Annual Leave", true, true,
            MondayToFriday,
            new Dictionary<Guid, IReadOnlyList<LeaveWorkingWeekDay>>(),
            [],
            []));
        var service = new LeaveRequestService(repository);

        await service.SubmitAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new SubmitLeaveRequest(leaveTypeId,
                new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 9),
                true, " am ", "  Personal work  "));

        Assert.Equal("AM", repository.Request!.HalfDayPeriod);
        Assert.Equal("Personal work", repository.Request.Reason);
        Assert.Equal(0.5m, repository.DaysRequested);
    }

    [Fact]
    public async Task SubmitAsync_RejectsHalfDayWhenLeaveTypeDisallowsIt()
    {
        var leaveTypeId = Guid.NewGuid();
        var repository = new FakeRepository(new LeaveRequestCalculationContext(
            Guid.NewGuid(), leaveTypeId, "ANNUAL", "Annual Leave", true, false,
            MondayToFriday,
            new Dictionary<Guid, IReadOnlyList<LeaveWorkingWeekDay>>(),
            [],
            []));
        var service = new LeaveRequestService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new SubmitLeaveRequest(leaveTypeId,
                new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 9),
                true, "AM", "Personal work")));
    }

    private static readonly IReadOnlyList<LeaveWorkingWeekDay> MondayToFriday =
        Enumerable.Range(1, 7)
            .Select(day => new LeaveWorkingWeekDay((byte)day, day <= 5))
            .ToArray();

    private sealed class FakeRepository(LeaveRequestCalculationContext context) : ILeaveRequestRepository
    {
        public SubmitLeaveRequest? Request { get; private set; }
        public decimal DaysRequested { get; private set; }
        public IReadOnlyList<LeaveYearReservationDto>? Allocations { get; private set; }

        public Task<LeaveRequestCalculationContext> GetCalculationContextAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveTypeId,
            DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
            Task.FromResult(context);

        public Task<LeaveRequestSubmissionResult> SubmitCalculatedAsync(
            Guid tenantId, Guid companyId, Guid actorUserId, SubmitLeaveRequest request,
            decimal daysRequested, IReadOnlyList<LeaveYearReservationDto> yearAllocations,
            IReadOnlyList<DateOnly> excludedDates, CancellationToken cancellationToken)
        {
            Request = request;
            DaysRequested = daysRequested;
            Allocations = yearAllocations;
            var dto = new LeaveRequestDto(
                Guid.NewGuid(), companyId, context.EmployeeId, request.LeaveTypeId,
                context.LeaveTypeCode, context.LeaveTypeName, request.FromDate, request.ToDate,
                request.IsHalfDay, request.HalfDayPeriod, daysRequested, request.Reason,
                "Pending", DateTime.UtcNow, new byte[8]);
            return Task.FromResult(new LeaveRequestSubmissionResult(dto, yearAllocations, excludedDates));
        }
    }
}
