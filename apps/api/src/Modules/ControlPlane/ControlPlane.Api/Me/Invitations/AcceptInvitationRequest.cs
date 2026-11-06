namespace ControlPlane.Api.Me.Invitations;

/// <summary>
/// The invitation code from the invitation link, and the name of a person who is not a user yet, within the API's limit. The API
/// trims the name before it counts its length. A known user sends none.
/// </summary>
public sealed record AcceptInvitationRequest(string Code, string? Name = null);
