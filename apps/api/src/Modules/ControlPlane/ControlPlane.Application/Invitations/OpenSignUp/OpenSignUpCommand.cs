using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Invitations.OpenSignUp;

// The first message of an invitation's sign-up. The identity provider is invite-only, so a person without an account can sign up only
// through the identity provider's own invitation. ControlPlane commits its invitation first; then a handler without a transaction asks
// the identity provider to open the sign-up and answer its link, another records the identity provider's invitation on ours and hands
// the link to Notifications, and a third withdraws the identity provider's invitation when ours ends. Each runs in the invitation's
// tenant, which the envelope carries. A tenant's first Owner, an Owner the platform invites and a person a member invites take the same
// path (section 6, "Invitation sign-up").
/// <summary>Opens the sign-up for an invited address. Sent with the invitation.</summary>
[MessageName("open-sign-up")]
public sealed record OpenSignUpCommand(Guid TenantId, Guid InvitationId, string Email, string TenantName, Uri AcceptLink, DateTimeOffset ExpiresAt)
{
    /// <summary>The sign-up of a new invitation of the tenant, with the accept link that carries the invitation's code.</summary>
    internal static OpenSignUpCommand For(Tenant tenant, Invitation invitation, Uri acceptLink) =>
        new(tenant.Id, invitation.Id, invitation.Email ?? throw new InvalidOperationException("A pending invitation always has an address."), tenant.Name, acceptLink, invitation.ExpiresAt);
}
