using HRMS.Application.Employees;
using Xunit;

namespace HRMS.Infrastructure.Tests.Employees;


public sealed class EmployeeServiceTests
{
    [Fact]
    public async Task CreateAsync_NormalizesEmployeeCodeAndEmail()
    {
        var repository = new RecordingEmployeeRepository();
        var service = new EmployeeService(repository);
        var request = new CreateEmployeeRequest(" emp-001 ", " Ada ", null, " Lovelace ", null, "ADA@EXAMPLE.COM", new DateTime(2026, 1, 2));

        await service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.NotNull(repository.Created);
        Assert.Equal("EMP-001", repository.Created.EmployeeCode);
        Assert.Equal("Ada", repository.Created.FirstName);
        Assert.Equal("Lovelace", repository.Created.LastName);
        Assert.Equal("ada@example.com", repository.Created.WorkEmail);
        Assert.Equal(new DateTime(2026, 1, 2), repository.Created.HireDate);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidWorkEmail()
    {
        var service = new EmployeeService(new RecordingEmployeeRepository());
        var request = new CreateEmployeeRequest("E1", "A", null, "B", null, "not-an-email", DateTime.Today);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), request));
    }

    [Fact]
    public async Task UpdateAsync_RequiresEightByteRowVersion()
    {
        var service = new EmployeeService(new RecordingEmployeeRepository());
        var request = new UpdateEmployeeRequest("A", null, "B", null, null, DateTime.Today, [1, 2]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request));
    }

    [Fact]
    public async Task UpdateStatusAsync_RequiresTerminationDateForTerminatedStatus()
    {
        var service = new EmployeeService(new RecordingEmployeeRepository());
        var request = new UpdateEmployeeStatusRequest("Terminated", null, new byte[8]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateStatusAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request));
    }

    [Fact]
    public async Task GetAllAsync_RejectsUnknownStatus()
    {
        var service = new EmployeeService(new RecordingEmployeeRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAllAsync(Guid.NewGuid(), Guid.NewGuid(), null, "Archived"));
    }

    private sealed class RecordingEmployeeRepository : IEmployeeRepository
    {
        public CreateEmployeeRequest? Created { get; private set; }
        public Task<IReadOnlyList<EmployeeDto>> GetAllAsync(Guid tenantId, Guid companyId, string? search, string? status, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmployeeDto>>([]);
        public Task<EmployeeDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid employeeId, CancellationToken cancellationToken) => Task.FromResult<EmployeeDto?>(null);
        public Task<EmployeeDto> CreateAsync(Guid tenantId, Guid companyId, CreateEmployeeRequest request, CancellationToken cancellationToken) { Created = request; return Task.FromResult<EmployeeDto>(null!); }
        public Task<EmployeeDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid employeeId, UpdateEmployeeRequest request, CancellationToken cancellationToken) => Task.FromResult<EmployeeDto?>(null);
        public Task<bool> UpdateStatusAsync(Guid tenantId, Guid companyId, Guid employeeId, UpdateEmployeeStatusRequest request, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
