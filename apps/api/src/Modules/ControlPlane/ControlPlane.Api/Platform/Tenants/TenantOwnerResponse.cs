namespace ControlPlane.Api.Platform.Tenants;

/// <summary>An Owner of a tenant: the user's id, name and address.</summary>
public sealed record TenantOwnerResponse(Guid UserId, string? Name, string? Email);
