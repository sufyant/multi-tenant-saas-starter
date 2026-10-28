namespace ControlPlane.Application.Plans;

/// <summary>
/// A plan as the platform reads it: its name, whether it is the default plan, how many tenants are on it (a purged tenant included, whose
/// tombstone keeps its plan), its value of each key it has one for, and when it was created.
/// </summary>
public sealed record PlanView(
    Guid Id,
    string Name,
    bool IsDefault,
    int TenantCount,
    IReadOnlyDictionary<string, EntitlementValueView> Entitlements,
    DateTimeOffset CreatedAt);
