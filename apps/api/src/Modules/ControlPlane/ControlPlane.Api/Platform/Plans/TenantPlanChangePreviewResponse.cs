namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// What a change of the tenant's plan would put it over: each key with the count and the new value, empty when it stays within every
/// key, and the keys an override of the tenant still holds, which the change does not touch.
/// </summary>
public sealed record TenantPlanChangePreviewResponse(IReadOnlyList<ExcessResponse> Excesses, IReadOnlyList<string> OverriddenKeys);
