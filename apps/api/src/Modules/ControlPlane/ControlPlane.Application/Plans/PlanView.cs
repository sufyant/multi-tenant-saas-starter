namespace ControlPlane.Application.Plans;

/// <summary>
/// A plan as the platform reads it: its name, whether it is the default plan, how many tenants are on it (a purged tenant included, whose
/// tombstone keeps its plan), its value of each key it has one for, its prices, its setting of each meter it sets, by key, and when it was
/// created. A saved idempotent result from before step 40 has no prices and no meters, so both may be null: such a plan is free with
/// no meter (I5).
/// </summary>
public sealed record PlanView(
    Guid Id,
    string Name,
    bool IsDefault,
    int TenantCount,
    IReadOnlyDictionary<string, EntitlementValueView> Entitlements,
    PlanPricesView? Prices,
    IReadOnlyList<MeterSettingView>? Meters,
    DateTimeOffset CreatedAt);
