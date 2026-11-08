namespace HRMS.Application.LeaveTypes;

/// <summary>Tenant- and company-scoped leave-type persistence operations.</summary>
public interface ILeaveTypeRepository
{
    Task<IReadOnlyList<LeaveTypeDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken);
    Task<LeaveTypeDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid leaveTypeId, CancellationToken cancellationToken);
    Task<LeaveTypeDto?> GetByCodeAsync(Guid tenantId, Guid companyId, string leaveTypeCode, CancellationToken cancellationToken);
    Task<LeaveTypeDto> CreateAsync(Guid tenantId, Guid companyId, CreateLeaveTypeRequest request, CancellationToken cancellationToken);
    Task<LeaveTypeDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid leaveTypeId, UpdateLeaveTypeRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid leaveTypeId, byte[] rowVersion, CancellationToken cancellationToken);
}
