using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Abstractions.Security;
using HRMS.Application.Companies;
using HRMS.Infrastructure.Persistence;
using HRMS.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IDatabaseMigrator, SqlDatabaseMigrator>();
        services.AddSingleton<IPasswordHasher, AspNetCorePasswordHasher>();

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICompanyService, CompanyService>();

        return services;
    }
}
