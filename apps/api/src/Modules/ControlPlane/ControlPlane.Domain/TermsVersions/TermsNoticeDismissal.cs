namespace ControlPlane.Domain.TermsVersions;

/// <summary>
/// A user closed the notice of a terms version that requires no acceptance, at this moment, so the notice shows no more. One for each user
/// and version. It is the person's own view, as a notification's read state is, so it records no acceptance.
/// </summary>
internal sealed class TermsNoticeDismissal(Guid versionId, Guid userId, DateTimeOffset dismissedAt)
{
    public Guid VersionId { get; private init; } = versionId;

    public Guid UserId { get; private init; } = userId;

    public DateTimeOffset DismissedAt { get; private init; } = dismissedAt;
}
