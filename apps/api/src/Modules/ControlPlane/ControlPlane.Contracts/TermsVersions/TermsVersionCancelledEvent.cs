using SharedKernel;

namespace ControlPlane.Contracts.TermsVersions;

/// <summary>
/// A system admin cancelled the scheduled terms version before it took effect. Its acceptances stay with it. Published through the outbox,
/// without a tenant.
/// </summary>
/// <param name="CancelledBy">The user id of the system admin who cancelled the version.</param>
/// <param name="AcceptanceCount">How many users had accepted the version, whatever their state.</param>
[MessageName("terms-version-cancelled")]
public sealed record TermsVersionCancelledEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid VersionId,
    Guid CancelledBy,
    DateOnly EffectiveDate,
    string Url,
    bool RequiresAcceptance,
    int AcceptanceCount);
