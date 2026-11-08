namespace HRMS.Application.Abstractions.Security;

public enum RefreshTokenRotationStatus
{
    Rotated = 1,
    Invalid = 2,
    ReuseDetected = 3
}
