namespace ControlPlane.Domain.TermsVersions;

/// <summary>What a signed-in person must do about the terms, and the version it is about; none for <see cref="TermsAction.None"/>.</summary>
internal sealed record TermsAsk(TermsAction Action, Guid? VersionId)
{
    public static readonly TermsAsk Nothing = new(TermsAction.None, null);
}
