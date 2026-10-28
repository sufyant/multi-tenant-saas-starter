using ControlPlane.Application.Plans;

namespace ControlPlane.Application.Tenants.GetTenantPlanChangePreview;

/// <summary>
/// Before a tenant's plan changes: every key the new plan would put the tenant over, with the count and the new value, and the keys that
/// an override of the tenant still holds, which the change does not touch. No excess when the tenant stays within every key.
/// </summary>
public sealed record TenantPlanChangePreviewView(IReadOnlyList<ExcessView> Excesses, IReadOnlyList<string> OverriddenKeys);
