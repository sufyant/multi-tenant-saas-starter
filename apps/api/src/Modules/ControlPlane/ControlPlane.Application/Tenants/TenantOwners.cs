using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Tenants;

/// <summary>An Owner as the platform's screens show them: the catalog user, with their names and address (A7).</summary>
public sealed record TenantOwnerView(Guid UserId, string? FirstName, string? LastName, string? Email)
{
    internal static TenantOwnerView Of(User user) => new(user.Id, user.FirstName, user.LastName, user.Email);
}

/// <summary>
/// The latest Owner invitation of a tenant that has no Owner: its address, its expiry, and its state at the moment of the request,
/// ControlPlane's name of it (<c>Pending</c>, <c>Expired</c>, <c>Cancelled</c>).
/// </summary>
public sealed record OwnerInvitationView(string Email, DateTimeOffset ExpiresAt, string State)
{
    internal static OwnerInvitationView? Of(Tenant tenant, DateTimeOffset now) =>
        tenant.OwnerInvitationStateAt(now) is { } state ? new(tenant.OwnerInvitationEmail!, tenant.OwnerInvitationExpiresAt!.Value, state.ToString()) : null;
}
