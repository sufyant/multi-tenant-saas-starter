namespace ControlPlane.Application.TermsVersions.GetMyTerms;

/// <summary>
/// What the signed-in person must do about the terms, ControlPlane's name of a <c>TermsAction</c>, and the version it is about; none when
/// they must do nothing.
/// </summary>
public sealed record MyTermsView(string Action, MyTermsVersionView? Version);
