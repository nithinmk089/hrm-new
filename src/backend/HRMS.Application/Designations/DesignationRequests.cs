namespace HRMS.Application.Designations;

public sealed record CreateDesignationRequest(
    string DesignationCode,
    string DesignationName,
    string? Description,
    short? JobLevel);

public sealed record UpdateDesignationRequest(
    string DesignationName,
    string? Description,
    short? JobLevel,
    byte[] RowVersion);

public sealed record DeactivateDesignationRequest(byte[] RowVersion);
