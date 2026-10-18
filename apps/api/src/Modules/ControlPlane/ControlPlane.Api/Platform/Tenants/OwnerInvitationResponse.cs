using ControlPlane.Api.Tenant.Invitations;

namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// The latest Owner invitation of a tenant that has no Owner: its address, its expiry, and its state at the moment of the request:
/// <c>pending</c>, <c>expired</c> once its expiry has passed, or <c>cancelled</c>. It is never <c>accepted</c>, because it is shown only
/// while the tenant has no Owner.
/// </summary>
public sealed record OwnerInvitationResponse(string Email, DateTimeOffset ExpiresAt, InvitationResponseState State);
