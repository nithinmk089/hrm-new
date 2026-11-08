using Microsoft.AspNetCore.Authorization;

namespace HRMS.API.Authorization;

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;
