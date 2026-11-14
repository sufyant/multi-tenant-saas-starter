namespace ControlPlane.Api.Me.Invitations;

/// <summary>
/// The invitation code from the invitation link, and the name of a person who is not a user yet, within the API's limit. The API
/// trims the name before it counts its length. A known user sends none. <c>termsVersionId</c> is the id of the terms version in force that
/// the person accepts with the invitation, or null; another version answers 409 <c>terms_version.changed</c>, and an id no version has 404
/// <c>terms_version.not_found</c>.
/// </summary>
public sealed record AcceptInvitationRequest(string Code, string? Name = null, Guid? TermsVersionId = null);
