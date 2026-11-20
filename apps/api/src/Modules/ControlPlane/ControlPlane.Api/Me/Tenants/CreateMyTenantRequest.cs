namespace ControlPlane.Api.Me.Tenants;

/// <summary>A new tenant of the signed-in user: its name and its slug, with the rules of a tenant a system admin creates. The API trims the name before it counts its length.</summary>
public sealed record CreateMyTenantRequest(string Name, string Slug);
