using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// The latest Owner invitation of a tenant that has no Owner: its address, its expiry, and its state at the moment of the request,
/// ControlPlane's name of it (<c>Pending</c>, <c>Expired</c>, <c>Cancelled</c>).
/// </summary>
public sealed record OwnerInvitationView(string Email, DateTimeOffset ExpiresAt, string State)
{
    internal static OwnerInvitationView? Of(Tenant tenant, DateTimeOffset now) =>
        tenant.OwnerInvitationStateAt(now) is { } state ? new(tenant.OwnerInvitationEmail!, tenant.OwnerInvitationExpiresAt!.Value, state.ToString()) : null;
}
