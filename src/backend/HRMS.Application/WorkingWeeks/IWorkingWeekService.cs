namespace HRMS.Application.WorkingWeeks;

public interface IWorkingWeekService
{
    Task<WorkingWeekDto> GetAsync(
        Guid tenantId, Guid companyId, Guid? officeId, CancellationToken cancellationToken = default);

    Task<WorkingWeekDto> SaveAsync(
        Guid tenantId, Guid companyId, Guid? officeId,
        SaveWorkingWeekRequest request, CancellationToken cancellationToken = default);
}
