using SharedKernel;

namespace ControlPlane.Contracts.TermsVersions;

/// <summary>
/// A system admin created a terms version. Published through the outbox, without a tenant: a version belongs to none. A version that emails
/// the users tells every user who may accept.
/// </summary>
/// <param name="CreatedBy">The user id of the system admin who created the version.</param>
/// <param name="EffectiveDate">The day the version takes effect, from 00:00 UTC.</param>
/// <param name="SendsEmail">Whether the creation emails every user who may accept; true in a message of the version before.</param>
[MessageName("terms-version-created")]
public sealed record TermsVersionCreatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid VersionId,
    Guid CreatedBy,
    DateOnly EffectiveDate,
    string Url,
    string? Summary,
    bool RequiresAcceptance,
    bool SendsEmail = true);
