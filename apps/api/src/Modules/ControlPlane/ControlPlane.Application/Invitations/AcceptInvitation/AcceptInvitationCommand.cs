namespace ControlPlane.Application.Invitations.AcceptInvitation;

/// <summary>
/// Accepts an invitation in its tenant, the one its invitation code names: the invitation is found by the hash of the code's
/// secret, and the catalog user, when new, and the membership are created in the same transaction as the invitation is used up.
/// The user's verified email addresses are read from the identity provider before that transaction begins, so no call leaves the
/// process while the invitation is locked. A new catalog user gets the invited address, the names the person typed (A7) and the
/// invitation's language; a user the catalog knows needs no names and keeps their language.
/// </summary>
public sealed record AcceptInvitationCommand(string ActorId, string Secret, IReadOnlyList<string> VerifiedEmails, string? FirstName, string? LastName);
