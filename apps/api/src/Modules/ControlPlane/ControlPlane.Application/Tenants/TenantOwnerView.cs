using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Tenants;

/// <summary>An Owner as the platform's screens show them: the catalog user, with their names and address (A7).</summary>
public sealed record TenantOwnerView(Guid UserId, string? FirstName, string? LastName, string? Email)
{
    internal static TenantOwnerView Of(User user) => new(user.Id, user.FirstName, user.LastName, user.Email);
}
