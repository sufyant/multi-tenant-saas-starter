namespace ControlPlane.Domain.FeatureFlags;

/// <summary>
/// One line of a flag's history (12.05): what changed, from what to what, when, and by whom. A deploy's line names its commit and no
/// person; a system admin's names the admin by user id. A line is only added, never changed. The platform's Activity log records the same
/// change too, from the change's event (K3); the history is the flag's own record, which its page and its cleanup read.
/// </summary>
internal sealed class FeatureFlagChange
{
    private FeatureFlagChange(Guid id, Guid flagId, FeatureFlagChangeKind kind, string? from, string? to, Guid? tenantId, DateTimeOffset at, Guid? actorId, string? revision)
    {
        Id = id;
        FlagId = flagId;
        Kind = kind;
        From = from;
        To = to;
        TenantId = tenantId;
        At = at;
        ActorId = actorId;
        Revision = revision;
    }

    public Guid Id { get; private init; }

    public Guid FlagId { get; private init; }

    public FeatureFlagChangeKind Kind { get; private init; }

    /// <summary>The rule's value before: a percentage's digits, or <c>on</c> or <c>off</c>; none for an organisation or a deploy.</summary>
    public string? From { get; private init; }

    /// <summary>The rule's value after, in the form of <see cref="From"/>.</summary>
    public string? To { get; private init; }

    /// <summary>The organisation listed or removed; none for any other change.</summary>
    public Guid? TenantId { get; private init; }

    public DateTimeOffset At { get; private init; }

    /// <summary>The user id of the system admin who changed the rule; none for a deploy.</summary>
    public Guid? ActorId { get; private init; }

    /// <summary>The commit of the deploy that added or removed the flag, when the deploy names one; none for a rule's change.</summary>
    public string? Revision { get; private init; }

    public static FeatureFlagChange OfDeploy(Guid flagId, FeatureFlagChangeKind kind, DateTimeOffset at, string? revision) =>
        new(Guid.CreateVersion7(at), flagId, kind, null, null, null, at, null, revision);

    public static FeatureFlagChange OfRule(Guid flagId, FeatureFlagChangeKind kind, string? from, string? to, Guid? tenantId, DateTimeOffset at, Guid actorId) =>
        new(Guid.CreateVersion7(at), flagId, kind, from, to, tenantId, at, actorId, null);
}
