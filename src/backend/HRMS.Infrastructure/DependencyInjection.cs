using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Abstractions.Security;
using HRMS.Application.Companies;
using HRMS.Application.Departments;
using HRMS.Application.Offices;
using HRMS.Application.Designations;
using HRMS.Application.Holidays;
using HRMS.Application.LeaveTypes;
using HRMS.Application.Security;
using HRMS.Infrastructure.Persistence;
using HRMS.Infrastructure.Security;
using HRMS.Infrastructure.Seeding;
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
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddScoped<IUserIdentityRepository, UserIdentityRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IPermissionAuthorizationRepository, PermissionAuthorizationRepository>();
        services.AddScoped<IPermissionAuthorizationService, PermissionAuthorizationService>();
        services.AddSingleton(new RefreshTokenSettings
        {
            LifetimeDays = configuration.GetValue<int?>(
                "Authentication:RefreshToken:LifetimeDays") ?? RefreshTokenService.DefaultLifetimeDays
        });
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddScoped<IDevelopmentSeedService, DevelopmentSeedService>();

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICompanyService, CompanyService>();

        services.AddScoped<IApiErrorLogRepository, ApiErrorLogRepository>();

        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IOfficeRepository, OfficeRepository>();
        services.AddScoped<IOfficeService, OfficeService>();
        services.AddScoped<IDesignationRepository, DesignationRepository>();
        services.AddScoped<IDesignationService, DesignationService>();
        services.AddScoped<IHolidayRepository, HolidayRepository>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<ILeaveTypeRepository, LeaveTypeRepository>();
        services.AddScoped<ILeaveTypeService, LeaveTypeService>();

        return services;
    }
}
