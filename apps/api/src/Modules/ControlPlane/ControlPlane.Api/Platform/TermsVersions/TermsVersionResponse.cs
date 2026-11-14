namespace ControlPlane.Api.Platform.TermsVersions;

/// <summary>
/// A terms version: its day, from 00:00 UTC, its link, its summary or null, whether it requires acceptance, its state on the day of the
/// request, how many users who may accept accepted it, whether anyone accepted it, which locks its link and <c>requiresAcceptance</c>, when
/// it was created, and when it was cancelled, or null.
/// </summary>
public sealed record TermsVersionResponse(
    Guid Id,
    DateOnly EffectiveDate,
    string Url,
    string? Summary,
    bool RequiresAcceptance,
    TermsVersionResponseState State,
    int AcceptedCount,
    bool Locked,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt);
