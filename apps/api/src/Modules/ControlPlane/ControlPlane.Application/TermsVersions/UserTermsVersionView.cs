namespace ControlPlane.Application.TermsVersions;

/// <summary>A terms version as one user's page shows it: its day, whether it requires acceptance, and when the user accepted it, or none.</summary>
public sealed record UserTermsVersionView(Guid VersionId, DateOnly EffectiveDate, bool RequiresAcceptance, DateTimeOffset? AcceptedAt);
