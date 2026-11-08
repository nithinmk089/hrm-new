namespace HRMS.Application.LeaveBalances;

public sealed class EmployeeLeaveBalanceService(IEmployeeLeaveBalanceRepository repository) : IEmployeeLeaveBalanceService
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "Opening", "Accrual", "CarryForward", "Adjustment" };

    public Task<IReadOnlyList<EmployeeLeaveBalanceDto>> GetAllAsync(
        Guid tenantId, Guid companyId, Guid employeeId, short leaveYear, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId, employeeId);
        EnsureYear(leaveYear);
        return repository.GetAllAsync(tenantId, companyId, employeeId, leaveYear, cancellationToken);
    }

    public Task<EmployeeLeaveBalanceDto> ApplyTransactionAsync(
        Guid tenantId, Guid companyId, Guid employeeId, Guid actorUserId,
        ApplyLeaveBalanceTransactionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId, employeeId);
        EnsureId(actorUserId, nameof(actorUserId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureId(request.LeaveTypeId, nameof(request.LeaveTypeId));
        EnsureYear(request.LeaveYear);
        if (!SupportedTypes.Contains(request.TransactionType?.Trim() ?? string.Empty))
            throw new ArgumentException("TransactionType must be Opening, Accrual, CarryForward or Adjustment.", nameof(request.TransactionType));
        if (request.DaysDelta == 0 || request.DaysDelta is < -366 or > 366)
            throw new ArgumentException("DaysDelta must be non-zero and within -366 to 366.", nameof(request.DaysDelta));
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (notes?.Length > 500)
            throw new ArgumentException("Notes cannot exceed 500 characters.", nameof(request.Notes));

        var normalized = request with { TransactionType = request.TransactionType.Trim(), Notes = notes };
        return repository.ApplyTransactionAsync(tenantId, companyId, employeeId, actorUserId, normalized, cancellationToken);
    }

    private static void EnsureScope(Guid tenantId, Guid companyId, Guid employeeId)
    {
        EnsureId(tenantId, nameof(tenantId));
        EnsureId(companyId, nameof(companyId));
        EnsureId(employeeId, nameof(employeeId));
    }

    private static void EnsureId(Guid id, string name)
    {
        if (id == Guid.Empty) throw new ArgumentException("A valid identifier is required.", name);
    }

    private static void EnsureYear(short year)
    {
        if (year is < 2000 or > 2100)
            throw new ArgumentOutOfRangeException(nameof(year), "LeaveYear must be between 2000 and 2100.");
    }
}
