using SharedKernel;

namespace ControlPlane.Application.Invitations.OpenOwnerSignUp;

// The first message of an invitation's sign-up, with OpenMemberSignUpCommand. The identity provider is invite-only, so a person
// without an account can sign up only through the identity provider's own invitation. ControlPlane commits its invitation first; then
// a handler without a transaction asks the identity provider to open the sign-up and answer its link, another records the identity
// provider's invitation on ours and hands the link to Notifications, and a third withdraws the identity provider's invitation when ours
// ends. Each runs in the invitation's tenant, which the envelope carries. The owner of a new tenant and a member have a message each, so
// the tenant creation saga hears of a failure for its owner only (S13).
/// <summary>Opens the sign-up for a new tenant's first owner. Sent by the tenant's activation.</summary>
[MessageName("open-owner-sign-up")]
public sealed record OpenOwnerSignUpCommand(Guid TenantId, Guid InvitationId, string Email, string TenantName, Uri AcceptLink, DateTimeOffset ExpiresAt);
