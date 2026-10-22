using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Invitations.OpenMemberSignUp;

/// <summary>Opens the sign-up for a person a member invited. Sent with the invitation.</summary>
[MessageName("open-member-sign-up")]
public sealed record OpenMemberSignUpCommand(Guid TenantId, Guid InvitationId, string Email, string TenantName, Uri AcceptLink, DateTimeOffset ExpiresAt)
{
    /// <summary>The sign-up of a new invitation of the tenant, with the accept link that carries the invitation's code.</summary>
    internal static OpenMemberSignUpCommand For(Tenant tenant, Invitation invitation, Uri acceptLink) =>
        new(tenant.Id, invitation.Id, invitation.Email, tenant.Name, acceptLink, invitation.ExpiresAt);
}
