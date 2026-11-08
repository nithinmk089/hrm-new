using HRMS.Application.WorkingWeeks;
using Xunit;

namespace HRMS.Infrastructure.Tests.WorkingWeeks;

public sealed class WorkingWeekServiceTests
{
    [Fact]
    public async Task SaveAsync_OrdersAllSevenDaysBeforePersistence()
    {
        var repository = new FakeRepository();
        var service = new WorkingWeekService(repository);
        var days = Enumerable.Range(1, 7).Reverse()
            .Select(day => new WorkingWeekDayDto((byte)day, day is >= 1 and <= 5)).ToArray();

        await service.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), null, new SaveWorkingWeekRequest(days));

        Assert.Equal(Enumerable.Range(1, 7).Select(day => (byte)day),
            repository.Saved!.Days.Select(day => day.DayOfWeek));
    }

    [Fact]
    public async Task SaveAsync_RejectsMissingWeekday()
    {
        var service = new WorkingWeekService(new FakeRepository());
        var days = Enumerable.Range(1, 6)
            .Select(day => new WorkingWeekDayDto((byte)day, true)).ToArray();

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(
            Guid.NewGuid(), Guid.NewGuid(), null, new SaveWorkingWeekRequest(days)));
    }

    [Fact]
    public async Task SaveAsync_RejectsDuplicateWeekday()
    {
        var service = new WorkingWeekService(new FakeRepository());
        var days = Enumerable.Range(1, 7)
            .Select(day => new WorkingWeekDayDto((byte)(day == 7 ? 6 : day), true)).ToArray();

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(
            Guid.NewGuid(), Guid.NewGuid(), null, new SaveWorkingWeekRequest(days)));
    }

    private sealed class FakeRepository : IWorkingWeekRepository
    {
        public SaveWorkingWeekRequest? Saved { get; private set; }

        public Task<WorkingWeekDto> GetAsync(Guid tenantId, Guid companyId, Guid? officeId, CancellationToken cancellationToken) =>
            Task.FromResult(new WorkingWeekDto(companyId, officeId, true, officeId is null, []));

        public Task<WorkingWeekDto> SaveAsync(Guid tenantId, Guid companyId, Guid? officeId, SaveWorkingWeekRequest request, CancellationToken cancellationToken)
        {
            Saved = request;
            return Task.FromResult(new WorkingWeekDto(companyId, officeId, true, false, request.Days));
        }
    }
}
