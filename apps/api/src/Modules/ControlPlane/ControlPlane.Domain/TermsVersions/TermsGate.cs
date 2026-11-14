namespace ControlPlane.Domain.TermsVersions;

/// <summary>
/// The gate after sign-in. A version that requires acceptance asks for it from the moment it is created, before its day: the version in
/// force first, because it applies now, then the scheduled one. Without such a request, a version that requires no acceptance gives a
/// notice until the person dismisses it or accepted it: the scheduled one first, as it is the newer, then the one in force. A notice tells a
/// person of a change, so a person who became a user after the version was created gets none: they never knew the earlier terms.
/// </summary>
internal static class TermsGate
{
    public static TermsAsk For(
        TermsGateVersion? current,
        TermsGateVersion? scheduled,
        IReadOnlySet<Guid> accepted,
        IReadOnlySet<Guid> dismissed,
        DateTimeOffset userCreatedAt)
    {
        foreach (var version in new[] { current, scheduled })
        {
            if (version is { RequiresAcceptance: true } && !accepted.Contains(version.Id))
            {
                return new TermsAsk(TermsAction.Accept, version.Id);
            }
        }

        foreach (var version in new[] { scheduled, current })
        {
            if (version is { RequiresAcceptance: false }
                && !accepted.Contains(version.Id)
                && !dismissed.Contains(version.Id)
                && userCreatedAt < version.CreatedAt)
            {
                return new TermsAsk(TermsAction.Notice, version.Id);
            }
        }

        return TermsAsk.Nothing;
    }
}
