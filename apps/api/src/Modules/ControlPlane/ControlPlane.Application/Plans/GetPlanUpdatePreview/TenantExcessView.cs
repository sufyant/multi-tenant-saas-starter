namespace ControlPlane.Application.Plans.GetPlanUpdatePreview;

/// <summary>
/// A tenant that a plan change would put over: its id, name and slug, how far over its furthest key it goes, every such key, and the keys
/// that an override of the tenant still holds, which the change does not touch.
/// </summary>
public sealed record TenantExcessView(Guid TenantId, string Name, string Slug, int Furthest, IReadOnlyList<ExcessView> Excesses, IReadOnlyList<string> OverriddenKeys);
