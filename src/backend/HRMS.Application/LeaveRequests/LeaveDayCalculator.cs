namespace HRMS.Application.LeaveRequests;

/// <summary>
/// Calculates chargeable leave dates using the company's configured working week,
/// effective-dated primary office assignments, office overrides and holidays.
/// </summary>
public static class LeaveDayCalculator
{
    private const int MaximumRequestCalendarDays = 366;

    public static LeaveDayCalculationResult Calculate(LeaveDayCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.CompanyWorkingWeek);
        ArgumentNullException.ThrowIfNull(request.OfficeWorkingWeeks);
        ArgumentNullException.ThrowIfNull(request.OfficeAssignments);
        ArgumentNullException.ThrowIfNull(request.Holidays);

        if (request.ToDate < request.FromDate)
            throw new ArgumentException("ToDate cannot be earlier than FromDate.", nameof(request));
        var calendarDays = request.ToDate.DayNumber - request.FromDate.DayNumber + 1;
        if (calendarDays > MaximumRequestCalendarDays)
            throw new ArgumentException($"A leave request cannot exceed {MaximumRequestCalendarDays} calendar days.", nameof(request));
        if (request.IsHalfDay && request.FromDate != request.ToDate)
            throw new ArgumentException("A half-day request must cover exactly one date.", nameof(request));
        if (request.IsHalfDay && !request.AllowHalfDay)
            throw new ArgumentException("The selected leave type does not allow half-day requests.", nameof(request));

        ValidateWeek(request.CompanyWorkingWeek, nameof(request.CompanyWorkingWeek));
        foreach (var (officeId, days) in request.OfficeWorkingWeeks)
        {
            if (officeId == Guid.Empty)
                throw new ArgumentException("Office working-week keys must be valid identifiers.", nameof(request));
            ValidateWeek(days, nameof(request.OfficeWorkingWeeks));
        }

        var assignments = request.OfficeAssignments
            .OrderBy(x => x.StartDate)
            .ToArray();
        for (var index = 0; index < assignments.Length; index++)
        {
            var assignment = assignments[index];
            if (assignment.EndDate is not null && assignment.EndDate < assignment.StartDate)
                throw new ArgumentException("An office assignment end date cannot precede its start date.", nameof(request));
            if (assignment.OfficeId == Guid.Empty)
                throw new ArgumentException("Office assignment identifiers must be valid.", nameof(request));
            if (index > 0 && (assignments[index - 1].EndDate is null ||
                assignments[index - 1].EndDate >= assignment.StartDate))
                throw new ArgumentException("Primary office assignments cannot overlap.", nameof(request));
        }

        var activeHolidays = request.Holidays
            .Where(x => x.IsActive)
            .GroupBy(x => x.Date)
            .ToDictionary(x => x.Key, x => x.Any(h => !h.IsOptional));

        var companyWeek = request.CompanyWorkingWeek.ToDictionary(x => x.DayOfWeek, x => x.IsWorkingDay);
        var officeWeeks = request.OfficeWorkingWeeks.ToDictionary(
            x => x.Key,
            x => x.Value.ToDictionary(day => day.DayOfWeek, day => day.IsWorkingDay));

        var chargeable = new List<DateOnly>();
        var excluded = new List<DateOnly>();
        for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            if (activeHolidays.TryGetValue(date, out var hasMandatoryHoliday) && hasMandatoryHoliday)
            {
                excluded.Add(date);
                continue;
            }

            var assignment = assignments.LastOrDefault(x =>
                x.StartDate <= date && (x.EndDate is null || x.EndDate >= date));
            var dayNumber = ToIsoDayOfWeek(date);
            var isWorkingDay = assignment?.OfficeId is Guid officeId &&
                               officeWeeks.TryGetValue(officeId, out var officeWeek)
                ? officeWeek[dayNumber]
                : companyWeek[dayNumber];

            var isOptionalHoliday = activeHolidays.ContainsKey(date);
            if (isWorkingDay || isOptionalHoliday)
                chargeable.Add(date);
            else
                excluded.Add(date);
        }

        var chargeableDays = request.IsHalfDay
            ? chargeable.Count == 1 ? 0.5m : 0m
            : chargeable.Count;

        return new LeaveDayCalculationResult(chargeableDays, chargeable, excluded);
    }

    private static void ValidateWeek(IReadOnlyList<LeaveWorkingWeekDay> days, string parameterName)
    {
        if (days.Count != 7 ||
            days.Select(x => x.DayOfWeek).Distinct().Count() != 7 ||
            days.Any(x => x.DayOfWeek is < 1 or > 7))
            throw new ArgumentException(
                "A configured working week must contain exactly one entry for each ISO weekday (1=Monday through 7=Sunday).",
                parameterName);
    }

    private static byte ToIsoDayOfWeek(DateOnly date) =>
        (byte)(((int)date.DayOfWeek + 6) % 7 + 1);
}
