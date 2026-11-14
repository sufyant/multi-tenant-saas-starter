namespace ControlPlane.Domain.TermsVersions;

/// <summary>
/// The state of every terms version on one day, which the days decide and nothing stores. A cancelled version is cancelled. Of the others,
/// one whose day has not come is scheduled; the latest whose day has come is in force; an older one is replaced. Two versions of one day are
/// ordered by their creation, so the later one is in force.
/// </summary>
internal sealed class TermsTimeline
{
    private readonly Dictionary<Guid, TermsVersionState> _states;

    private TermsTimeline(Dictionary<Guid, TermsVersionState> states, Guid? current, Guid? scheduled)
    {
        _states = states;
        CurrentId = current;
        ScheduledId = scheduled;
    }

    /// <summary>The version in force; none before the first version takes effect.</summary>
    public Guid? CurrentId { get; }

    /// <summary>The version that waits for its day; none when no version does. There is one at most.</summary>
    public Guid? ScheduledId { get; }

    public static TermsTimeline Of(IEnumerable<TermsVersionDay> versions, DateOnly today)
    {
        var states = new Dictionary<Guid, TermsVersionState>();
        Guid? current = null;
        Guid? scheduled = null;
        foreach (var version in versions.OrderByDescending(version => version.EffectiveDate).ThenByDescending(version => version.CreatedAt).ThenByDescending(version => version.Id))
        {
            var state = version.Cancelled ? TermsVersionState.Cancelled
                : version.EffectiveDate > today ? TermsVersionState.Scheduled
                : current is null ? TermsVersionState.Current
                : TermsVersionState.Replaced;
            current ??= state == TermsVersionState.Current ? version.Id : null;
            scheduled ??= state == TermsVersionState.Scheduled ? version.Id : null;
            states[version.Id] = state;
        }

        return new TermsTimeline(states, current, scheduled);
    }

    public TermsVersionState StateOf(Guid versionId) => _states[versionId];
}
