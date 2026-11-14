namespace ControlPlane.Application.Terms.ListTermsVersionPeople;

/// <summary>
/// A user who may accept, as the version's people read them: their name and address now, and when they accepted the version; none for one
/// who did not. <paramref name="CreatedAt"/> places a user who did not accept on the page.
/// </summary>
public sealed record TermsPersonView(Guid UserId, string? Name, string? Email, DateTimeOffset? AcceptedAt, DateTimeOffset CreatedAt);
