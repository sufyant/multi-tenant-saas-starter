namespace ControlPlane.Api.Platform.TermsVersions;

/// <summary>
/// A user who may accept a version: their id, their name and address now, either null when the user has none, whether they accepted it,
/// and when; null for one who did not.
/// </summary>
public sealed record TermsPersonResponse(Guid UserId, string? Name, string? Email, TermsPersonResponseStatus Status, DateTimeOffset? AcceptedAt);
