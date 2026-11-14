namespace ControlPlane.Domain.TermsVersions;

/// <summary>A terms version as its state reads it: its day, when it was created, and whether it was cancelled.</summary>
internal sealed record TermsVersionDay(Guid Id, DateOnly EffectiveDate, DateTimeOffset CreatedAt, bool Cancelled);
