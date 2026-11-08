namespace HRMS.Application.WorkingWeeks;

public interface IWorkingWeekRepository
{
    Task<WorkingWeekDto> GetAsync(
        Guid tenantId, Guid companyId, Guid? officeId, CancellationToken cancellationToken);

    Task<WorkingWeekDto> SaveAsync(
        Guid tenantId, Guid companyId, Guid? officeId,
        SaveWorkingWeekRequest request, CancellationToken cancellationToken);
}
