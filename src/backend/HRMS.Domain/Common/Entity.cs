namespace HRMS.Domain.Common;

/// <summary>
/// Base type for domain entities with identity.
/// Add domain-specific behavior to concrete aggregates rather than this base type.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }
}
