namespace HRMS.Application.Abstractions.Security;

/// <summary>Application settings controlling refresh-token lifetime.</summary>
public sealed class RefreshTokenSettings
{
    public int LifetimeDays { get; init; } = 14;
}
