namespace ControlPlane.Api.Me.Invitations;

/// <summary>
/// The invitation code from the invitation link, and the names of a person the catalog does not know yet, each within the API's limit.
/// The API trims each name before it counts its length. A user the catalog knows sends none.
/// </summary>
public sealed record AcceptInvitationRequest(string Code, string? FirstName = null, string? LastName = null);
