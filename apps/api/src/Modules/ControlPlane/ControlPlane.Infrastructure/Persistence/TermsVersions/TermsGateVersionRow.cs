namespace ControlPlane.Infrastructure.Persistence.TermsVersions;

/// <summary>One version's rows, as the timeline and the gate weigh them, and whether the user accepted it.</summary>
internal sealed record TermsGateVersionRow(
    Guid Id,
    DateOnly EffectiveDate,
    DateTimeOffset CreatedAt,
    bool Cancelled,
    bool RequiresAcceptance,
    bool SendsEmail,
    bool Accepted);
