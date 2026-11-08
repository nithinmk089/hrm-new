namespace HRMS.Application.LeaveTypes;

/// <summary>Use cases for company leave-type configuration.</summary>
public interface ILeaveTypeService
{
    Task<IReadOnlyList<LeaveTypeDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
    Task<LeaveTypeDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid leaveTypeId, CancellationToken cancellationToken = default);
    Task<LeaveTypeDto> CreateAsync(Guid tenantId, Guid companyId, CreateLeaveTypeRequest request, CancellationToken cancellationToken = default);
    Task<LeaveTypeDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid leaveTypeId, UpdateLeaveTypeRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid leaveTypeId, byte[] rowVersion, CancellationToken cancellationToken = default);
}
