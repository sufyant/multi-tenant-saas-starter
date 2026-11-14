namespace ControlPlane.Application.TermsVersions;

/// <summary>
/// A terms version as the platform reads it: its values; its state on the day of the request, ControlPlane's name of a
/// <c>TermsVersionState</c>; how many users who may accept accepted it; whether anyone accepted it, which locks its link and whether it
/// requires acceptance; when it was created; and when it was cancelled.
/// </summary>
public sealed record TermsVersionView(
    Guid Id,
    DateOnly EffectiveDate,
    string Url,
    string? Summary,
    bool RequiresAcceptance,
    string State,
    int AcceptedCount,
    bool Locked,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt);
