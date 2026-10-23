using ControlPlane.Api.Tenant.Invitations;
using SharedKernel;

namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// The latest Owner invitation of a tenant that has no Owner: its address, its expiry, and its state at the moment of the request:
/// <c>pending</c>, <c>expired</c> once its expiry has passed, or <c>cancelled</c>. It is never <c>accepted</c>, because it is shown only
/// while the tenant has no Owner. It carries the language the invitation keeps for its email (K8), so a client that invites the Owner
/// again can send it back.
/// </summary>
public sealed record OwnerInvitationResponse(string Email, DateTimeOffset ExpiresAt, InvitationResponseState State, Language Language);
