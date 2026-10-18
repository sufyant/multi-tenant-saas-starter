namespace ControlPlane.Api.Me.Invitations;

/// <summary>
/// The invitation code from the invitation link, and the names of a person who has not signed up yet, each within the API's limit.
/// The API trims each name before it counts its length. A known user sends none.
/// </summary>
public sealed record AcceptInvitationRequest(string Code, string? FirstName = null, string? LastName = null);
