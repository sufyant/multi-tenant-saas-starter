namespace ControlPlane.Api.Platform.Tenants;

/// <summary>An Owner of a tenant: the catalog user's id, names and address.</summary>
public sealed record TenantOwnerResponse(Guid UserId, string? FirstName, string? LastName, string? Email);
