using SharedKernel;

namespace ControlPlane.Contracts.Terms;

/// <summary>A system admin edited the scheduled terms version: its values before and after. Published through the outbox, without a tenant.</summary>
/// <param name="UpdatedBy">The user id of the system admin who edited the version.</param>
[MessageName("terms-version-updated")]
public sealed record TermsVersionUpdatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid VersionId,
    Guid UpdatedBy,
    TermsVersionRecord Old,
    TermsVersionRecord New);

/// <summary>The values of a terms version at one moment.</summary>
public sealed record TermsVersionRecord(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance);
