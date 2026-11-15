using System.Text.Json.Nodes;
using ControlPlane.Api.Platform.Plans;

namespace ControlPlane.Api.Tenant.Plans;

/// <summary>
/// A plan that the tenant can move to, as the Change plan page draws it (10.02): its name, whether it is the default plan, which a
/// cancellation moves to, its values by key as <c>GET /v1/tenants/{tenantId}</c> answers them (a feature true or false, a limit a number,
/// null for an unlimited limit; a key the plan has no value for is closed and missing), its prices in the deployment's one currency, an ISO
/// 4217 code, and its meters.
/// </summary>
public sealed record PlanChoiceResponse(
    Guid Id,
    string Name,
    bool IsDefault,
    IReadOnlyDictionary<string, JsonNode?> Entitlements,
    string Currency,
    PlanPricesResponse Prices,
    IReadOnlyList<MeterSettingResponse> Meters);
