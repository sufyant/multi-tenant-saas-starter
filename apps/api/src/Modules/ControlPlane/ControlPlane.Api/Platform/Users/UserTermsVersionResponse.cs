namespace ControlPlane.Api.Platform.Users;

/// <summary>A terms version for the user's page: its id and day, whether it requires acceptance, and when the user accepted it, null for not yet.</summary>
public sealed record UserTermsVersionResponse(Guid VersionId, DateOnly EffectiveDate, bool RequiresAcceptance, DateTimeOffset? AcceptedAt);
