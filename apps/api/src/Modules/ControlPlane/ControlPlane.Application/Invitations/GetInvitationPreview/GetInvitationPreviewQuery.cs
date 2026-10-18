namespace ControlPlane.Application.Invitations.GetInvitationPreview;

/// <summary>
/// What the accept screen shows before the person accepts: the invitation the secret names, in the tenant its code names, as accepting
/// finds it (section 6). The signed-in user's verified email addresses are read from the identity provider before the transaction
/// begins. It reads only: it changes nothing and publishes nothing, so no audit entry is written (K3).
/// </summary>
public sealed record GetInvitationPreviewQuery(string Secret, IReadOnlyList<string> VerifiedEmails);
