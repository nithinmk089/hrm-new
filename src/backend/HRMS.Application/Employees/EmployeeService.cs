namespace HRMS.Application.Employees;

public sealed class EmployeeService(IEmployeeRepository repository) : IEmployeeService
{
    private static readonly string[] Statuses = ["Preboarding", "Active", "OnLeave", "Inactive", "Terminated"];

    public Task<IReadOnlyList<EmployeeDto>> GetAllAsync(Guid tenantId, Guid companyId, string? search, string? status, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId);
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (normalizedSearch?.Length > 100) throw new ArgumentException("Search cannot exceed 100 characters.", nameof(search));
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        if (normalizedStatus is not null && !Statuses.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Employment status is not supported.", nameof(status));
        return repository.GetAllAsync(tenantId, companyId, normalizedSearch, normalizedStatus, cancellationToken);
    }

    public Task<EmployeeDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId); EnsureId(employeeId, nameof(employeeId));
        return repository.GetByIdAsync(tenantId, companyId, employeeId, cancellationToken);
    }

    public Task<EmployeeDto> CreateAsync(Guid tenantId, Guid companyId, CreateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId); ArgumentNullException.ThrowIfNull(request);
        var code = Required(request.EmployeeCode, nameof(request.EmployeeCode), 30).ToUpperInvariant();
        var first = Required(request.FirstName, nameof(request.FirstName), 100);
        var last = Required(request.LastName, nameof(request.LastName), 100);
        var middle = Optional(request.MiddleName, 100);
        var preferred = Optional(request.PreferredName, 100);
        var email = NormalizeEmail(request.WorkEmail);
        if (request.HireDate == default) throw new ArgumentException("HireDate is required.", nameof(request.HireDate));
        return repository.CreateAsync(tenantId, companyId,
            new CreateEmployeeRequest(code, first, middle, last, preferred, email, request.HireDate.Date), cancellationToken);
    }

    public Task<EmployeeDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid employeeId, UpdateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId); EnsureId(employeeId, nameof(employeeId)); ArgumentNullException.ThrowIfNull(request);
        if (request.RowVersion is not { Length: 8 }) throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(request.RowVersion));
        if (request.HireDate == default) throw new ArgumentException("HireDate is required.", nameof(request.HireDate));
        return repository.UpdateAsync(tenantId, companyId, employeeId,
            new UpdateEmployeeRequest(Required(request.FirstName, nameof(request.FirstName), 100),
                Optional(request.MiddleName, 100), Required(request.LastName, nameof(request.LastName), 100),
                Optional(request.PreferredName, 100), NormalizeEmail(request.WorkEmail), request.HireDate.Date, request.RowVersion),
            cancellationToken);
    }

    public Task<bool> UpdateStatusAsync(Guid tenantId, Guid companyId, Guid employeeId, UpdateEmployeeStatusRequest request, CancellationToken cancellationToken = default)
    {
        EnsureScope(tenantId, companyId); EnsureId(employeeId, nameof(employeeId)); ArgumentNullException.ThrowIfNull(request);
        if (request.RowVersion is not { Length: 8 }) throw new ArgumentException("A valid 8-byte RowVersion is required.", nameof(request.RowVersion));
        var status = Required(request.EmploymentStatus, nameof(request.EmploymentStatus), 20);
        var canonical = Statuses.FirstOrDefault(x => x.Equals(status, StringComparison.OrdinalIgnoreCase));
        if (canonical is null) throw new ArgumentException("Employment status is not supported.", nameof(request.EmploymentStatus));
        if ((canonical == "Terminated") != request.TerminationDate.HasValue)
            throw new ArgumentException("TerminationDate is required only when status is Terminated.", nameof(request.TerminationDate));
        return repository.UpdateStatusAsync(tenantId, companyId, employeeId,
            request with { EmploymentStatus = canonical, TerminationDate = request.TerminationDate?.Date }, cancellationToken);
    }

    private static void EnsureScope(Guid tenantId, Guid companyId) { EnsureId(tenantId, nameof(tenantId)); EnsureId(companyId, nameof(companyId)); }
    private static void EnsureId(Guid id, string name) { if (id == Guid.Empty) throw new ArgumentException("A valid identifier is required.", name); }
    private static string Required(string? value, string name, int max) { var v = value?.Trim(); if (string.IsNullOrWhiteSpace(v)) throw new ArgumentException($"{name} is required.", name); if (v.Length > max) throw new ArgumentException($"{name} cannot exceed {max} characters.", name); return v; }
    private static string? Optional(string? value, int max) { var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); if (v?.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters."); return v; }
    private static string? NormalizeEmail(string? value) { var email = Optional(value, 254); if (email is not null && (!System.Net.Mail.MailAddress.TryCreate(email, out var parsed) || !parsed.Address.Equals(email, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("WorkEmail must be a valid email address.", nameof(value)); return email?.ToLowerInvariant(); }
}
