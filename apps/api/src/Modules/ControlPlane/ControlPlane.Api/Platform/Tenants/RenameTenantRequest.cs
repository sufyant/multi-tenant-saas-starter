namespace ControlPlane.Api.Platform.Tenants;

/// <summary>A tenant's new name. The API trims it before it counts its length. The slug never changes.</summary>
public sealed record RenameTenantRequest(string Name);
