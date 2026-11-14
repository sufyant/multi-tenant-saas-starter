namespace ControlPlane.Application.Terms.GetTermsInForce;

/// <summary>The version in force, as anyone reads it before sign-in: its id, its day and its link.</summary>
public sealed record TermsInForceView(Guid Id, DateOnly EffectiveDate, string Url);
