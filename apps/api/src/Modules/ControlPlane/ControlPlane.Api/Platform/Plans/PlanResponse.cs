namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A plan: its name, whether it is the default plan, how many tenants are on it, its value of each key it has one for, its prices in the
/// deployment's one currency, an ISO 4217 code, its setting of each meter it sets, by key, and when it was created. A key the plan has no
/// value for is closed; a plan without a meter shows no Usage (step 40).
/// </summary>
public sealed record PlanResponse(
    Guid Id,
    string Name,
    bool IsDefault,
    int TenantCount,
    IReadOnlyDictionary<string, EntitlementValueResponse> Entitlements,
    string Currency,
    PlanPricesResponse Prices,
    IReadOnlyList<MeterSettingResponse> Meters,
    DateTimeOffset CreatedAt);
