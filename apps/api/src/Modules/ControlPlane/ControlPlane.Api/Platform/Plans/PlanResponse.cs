namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A plan: its name, whether it is the default plan, how many tenants are on it, its value of each key it has one for, and when it was
/// created. A key the plan has no value for is closed.
/// </summary>
public sealed record PlanResponse(
    Guid Id,
    string Name,
    bool IsDefault,
    int TenantCount,
    IReadOnlyDictionary<string, EntitlementValueResponse> Entitlements,
    DateTimeOffset CreatedAt);
