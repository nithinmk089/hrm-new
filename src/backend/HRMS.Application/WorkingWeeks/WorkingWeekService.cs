namespace HRMS.Application.WorkingWeeks;

/// <summary>Validates complete ISO-8601 working-week schedules.</summary>
public sealed class WorkingWeekService(IWorkingWeekRepository repository) : IWorkingWeekService
{
    public Task<WorkingWeekDto> GetAsync(
        Guid tenantId, Guid companyId, Guid? officeId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId, officeId);
        return repository.GetAsync(tenantId, companyId, officeId, cancellationToken);
    }

    public Task<WorkingWeekDto> SaveAsync(
        Guid tenantId, Guid companyId, Guid? officeId,
        SaveWorkingWeekRequest request, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId, officeId);
        ArgumentNullException.ThrowIfNull(request);
        if (officeId is null && request.Days is null)
            throw new ArgumentException("A complete seven-day schedule is required.", nameof(request));
        if (request.Days is null || request.Days.Count != 7 ||
            request.Days.Select(day => day.DayOfWeek).Distinct().Count() != 7 ||
            request.Days.Any(day => day.DayOfWeek is < 1 or > 7))
            throw new ArgumentException(
                "Provide exactly one entry for each ISO weekday (1=Monday through 7=Sunday).",
                nameof(request.Days));

        var normalized = new SaveWorkingWeekRequest(
            request.Days.OrderBy(day => day.DayOfWeek).ToArray());
        return repository.SaveAsync(tenantId, companyId, officeId, normalized, cancellationToken);
    }

    private static void EnsureScope(Guid tenantId, Guid companyId, Guid? officeId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A valid tenant is required.", nameof(tenantId));
        if (companyId == Guid.Empty) throw new ArgumentException("A valid company is required.", nameof(companyId));
        if (officeId == Guid.Empty) throw new ArgumentException("A valid office is required.", nameof(officeId));
    }
}
