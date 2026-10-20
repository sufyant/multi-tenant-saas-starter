using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// The latest Owner invitation of a tenant that has no Owner: its address, its expiry, its state at the moment of the request,
/// ControlPlane's name of it (<c>Pending</c>, <c>Expired</c>, <c>Cancelled</c>), and the language it keeps for its email (K8). The language
/// is null or out of date in a summary that the previous version wrote during a deploy (section 13 of ARCHITECTURE.md).
/// </summary>
public sealed record OwnerInvitationView(string Email, DateTimeOffset ExpiresAt, string State, Language? Language)
{
    internal static OwnerInvitationView? Of(Tenant tenant, DateTimeOffset now) =>
        tenant.OwnerInvitationStateAt(now) is { } state ? new(tenant.OwnerInvitationEmail!, tenant.OwnerInvitationExpiresAt!.Value, state.ToString(), tenant.OwnerInvitationLanguage) : null;
}
