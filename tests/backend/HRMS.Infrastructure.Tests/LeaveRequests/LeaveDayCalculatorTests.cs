using HRMS.Application.LeaveRequests;
using Xunit;

namespace HRMS.Infrastructure.Tests.LeaveRequests;

public sealed class LeaveDayCalculatorTests
{
    private static readonly IReadOnlyList<LeaveWorkingWeekDay> MondayToFriday =
        Enumerable.Range(1, 7)
            .Select(day => new LeaveWorkingWeekDay((byte)day, day <= 5))
            .ToArray();

    [Fact]
    public void Calculate_ExcludesWeeklyOffDaysAndMandatoryHolidays()
    {
        var request = CreateRequest(
            new DateOnly(2026, 11, 9),
            new DateOnly(2026, 11, 15),
            holidays: [new LeaveCalendarHoliday(new DateOnly(2026, 11, 11), false, true)]);

        var result = LeaveDayCalculator.Calculate(request);

        Assert.Equal(4m, result.ChargeableDays);
        Assert.Equal(
            new[] { new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 10),
                new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 13) },
            result.ChargeableDates);
        Assert.Equal(
            new[] { new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 15) },
            result.ExcludedDates);
    }

    [Fact]
    public void Calculate_OptionalHolidayIsChargeableEvenWhenItIsNormallyAWeeklyOff()
    {
        var request = CreateRequest(
            new DateOnly(2026, 11, 14),
            new DateOnly(2026, 11, 14),
            holidays: [new LeaveCalendarHoliday(new DateOnly(2026, 11, 14), true, true)]);

        var result = LeaveDayCalculator.Calculate(request);

        Assert.Equal(1m, result.ChargeableDays);
    }

    [Fact]
    public void Calculate_InactiveHolidayDoesNotChangeWorkingDayCount()
    {
        var request = CreateRequest(
            new DateOnly(2026, 11, 11),
            new DateOnly(2026, 11, 11),
            holidays: [new LeaveCalendarHoliday(new DateOnly(2026, 11, 11), false, false)]);

        Assert.Equal(1m, LeaveDayCalculator.Calculate(request).ChargeableDays);
    }

    [Fact]
    public void Calculate_UsesOfficeOverrideForEffectiveAssignmentDates()
    {
        var officeId = Guid.NewGuid();
        var officeWeek = Enumerable.Range(1, 7)
            .Select(day => new LeaveWorkingWeekDay((byte)day, day <= 4))
            .ToArray();
        var request = CreateRequest(
            new DateOnly(2026, 11, 9),
            new DateOnly(2026, 11, 13),
            officeWeeks: new Dictionary<Guid, IReadOnlyList<LeaveWorkingWeekDay>>
            {
                [officeId] = officeWeek
            },
            assignments: [new LeaveOfficeAssignment(new DateOnly(2026, 11, 11), null, officeId)]);

        var result = LeaveDayCalculator.Calculate(request);

        Assert.Equal(4m, result.ChargeableDays);
        Assert.Equal(new DateOnly(2026, 11, 13), Assert.Single(result.ExcludedDates));
    }

    [Fact]
    public void Calculate_ChargesHalfDayAsPointFive()
    {
        var request = CreateRequest(
            new DateOnly(2026, 11, 9),
            new DateOnly(2026, 11, 9),
            isHalfDay: true,
            allowHalfDay: true);

        Assert.Equal(0.5m, LeaveDayCalculator.Calculate(request).ChargeableDays);
    }

    [Fact]
    public void Calculate_RejectsHalfDayWhenLeaveTypeDisallowsIt()
    {
        var request = CreateRequest(
            new DateOnly(2026, 11, 9),
            new DateOnly(2026, 11, 9),
            isHalfDay: true,
            allowHalfDay: false);

        Assert.Throws<ArgumentException>(() => LeaveDayCalculator.Calculate(request));
    }

    [Fact]
    public void Calculate_RejectsHalfDayAcrossMultipleDates()
    {
        var request = CreateRequest(
            new DateOnly(2026, 11, 9),
            new DateOnly(2026, 11, 10),
            isHalfDay: true,
            allowHalfDay: true);

        Assert.Throws<ArgumentException>(() => LeaveDayCalculator.Calculate(request));
    }

    [Fact]
    public void Calculate_RejectsUnconfiguredCompanyWorkingWeek()
    {
        var request = CreateRequest(
            new DateOnly(2026, 11, 9),
            new DateOnly(2026, 11, 9),
            companyWeek: MondayToFriday.Take(6).ToArray());

        Assert.Throws<ArgumentException>(() => LeaveDayCalculator.Calculate(request));
    }

    [Fact]
    public void Calculate_RejectsOverlappingPrimaryOfficeAssignments()
    {
        var request = CreateRequest(
            new DateOnly(2026, 11, 9),
            new DateOnly(2026, 11, 13),
            assignments:
            [
                new LeaveOfficeAssignment(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 12), Guid.NewGuid()),
                new LeaveOfficeAssignment(new DateOnly(2026, 11, 10), null, Guid.NewGuid())
            ]);

        Assert.Throws<ArgumentException>(() => LeaveDayCalculator.Calculate(request));
    }

    private static LeaveDayCalculationRequest CreateRequest(
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyList<LeaveCalendarHoliday>? holidays = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<LeaveWorkingWeekDay>>? officeWeeks = null,
        IReadOnlyList<LeaveOfficeAssignment>? assignments = null,
        bool isHalfDay = false,
        bool allowHalfDay = false,
        IReadOnlyList<LeaveWorkingWeekDay>? companyWeek = null) =>
        new(
            fromDate,
            toDate,
            isHalfDay,
            allowHalfDay,
            companyWeek ?? MondayToFriday,
            officeWeeks ?? new Dictionary<Guid, IReadOnlyList<LeaveWorkingWeekDay>>(),
            assignments ?? [],
            holidays ?? []);
}
