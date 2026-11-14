namespace ControlPlane.Domain.Terms;

/// <summary>
/// A user accepted a terms version at this moment: the proof of which text they agreed to (GDPR, Article 7(1)). One for each user and
/// version. It stays when the version is cancelled, and when the user is deleted: the user row stays too, without personal data (A8).
/// </summary>
internal sealed class TermsAcceptance(Guid versionId, Guid userId, DateTimeOffset acceptedAt)
{
    public Guid VersionId { get; private init; } = versionId;

    public Guid UserId { get; private init; } = userId;

    public DateTimeOffset AcceptedAt { get; private init; } = acceptedAt;
}
