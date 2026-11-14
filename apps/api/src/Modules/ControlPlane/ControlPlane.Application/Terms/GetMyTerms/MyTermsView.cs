namespace ControlPlane.Application.Terms.GetMyTerms;

/// <summary>
/// What the signed-in person must do about the terms, ControlPlane's name of a <c>TermsAction</c>, and the version it is about; none when
/// they must do nothing.
/// </summary>
public sealed record MyTermsView(string Action, MyTermsVersionView? Version);

/// <summary>A terms version as the person reads it: its day, its link, its summary and whether it requires acceptance.</summary>
public sealed record MyTermsVersionView(Guid Id, DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance);
