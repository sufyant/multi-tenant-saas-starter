namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A tenant that a plan edit would put over: its id, name and slug, how far over its furthest key it goes, each such key, and the keys
/// that an override of the tenant still holds, which the edit does not touch.
/// </summary>
public sealed record TenantExcessResponse(Guid TenantId, string Name, string Slug, int Furthest, IReadOnlyList<ExcessResponse> Excesses, IReadOnlyList<string> OverriddenKeys);
