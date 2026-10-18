using Wolverine;

namespace ControlPlane.Application.Invitations.SignUp;

// The messages of an invitation's sign-up. Clerk is invite-only, so a person without an account can sign up only through Clerk's own
// invitation. ControlPlane commits its invitation first; then a handler without a transaction asks Clerk for the sign-up link, another
// records Clerk's invitation on ours and hands the link to Notifications, and a third withdraws Clerk's invitation when ours ends. Each
// runs in the invitation's tenant, which the envelope carries. The owner of a new tenant and a member have a message each, so the tenant
// creation saga hears of a failure for its owner only (S13).

/// <summary>Lets a new tenant's first owner sign up. Sent by the tenant's activation.</summary>
public sealed record OpenOwnerSignUpCommand(Guid TenantId, Guid InvitationId, string Email, string TenantName, Uri AcceptLink, DateTimeOffset ExpiresAt);

/// <summary>Lets a person a member invited sign up. Sent with the invitation.</summary>
public sealed record OpenMemberSignUpCommand(Guid TenantId, Guid InvitationId, string Email, string TenantName, Uri AcceptLink, DateTimeOffset ExpiresAt);

/// <summary>
/// The identity provider's answer: the link to send, and its invitation behind the link, or none for an address with an account.
/// </summary>
public sealed record RecordSignUpCommand(
    Guid TenantId,
    Guid InvitationId,
    string Email,
    string TenantName,
    Uri Link,
    string? SignUpInvitationId,
    DateTimeOffset ExpiresAt,
    bool ForFirstOwner);

/// <summary>Withdraws the identity provider's invitation: nobody can sign up with its link any more.</summary>
public sealed record WithdrawSignUpCommand(string SignUpInvitationId);

/// <summary>
/// Our invitation's lifetime is over: the identity provider's invitation, which counts its life in whole days, is withdrawn at the
/// same moment. Scheduled when the link goes out.
/// </summary>
public sealed record ExpireSignUpCommand(Guid TenantId, Guid InvitationId);
