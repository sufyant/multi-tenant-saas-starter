using System.Globalization;
using SharedKernel;

namespace ControlPlane.Domain.FeatureFlags;

/// <summary>
/// A feature flag of this environment: what the code defines (its key, type, default value and description), the day and the commit of
/// the deploy that first stored it or archived it, and its three rules. Rule 1 lists organisations; its rows are
/// <see cref="FeatureFlagTenant"/>, and the flag keeps their count. Rule 2 is a percentage of the other organisations, from 0 to 100.
/// Rule 3 turns the flag on or off for everyone else. Each rule changes on its own, only by a system admin, under the flag's lock (K6a).
/// A flag the code removed is archived: its rules no longer change, and its key is never used again. Each environment's platform manages
/// only its own flags, in its own database (docs/roadmap.md, section 7).
/// </summary>
internal sealed class FeatureFlag
{
    public const int DescriptionMaxLength = 500;

    public const int RevisionMaxLength = 64;

    public const int MaxPercentage = 100;

    /// <summary>
    /// How long a flag stays on for everyone, or off for every organisation, before its cleanup says so: long enough for an incident to
    /// show, short enough that a flag does not stay for months (docs/roadmap.md, section 7).
    /// </summary>
    public static readonly TimeSpan CleanupAfter = TimeSpan.FromDays(30);

    public static readonly Error NotFound = Error.NotFound(ControlPlaneErrorCodes.FeatureFlagNotFound, "The feature flag was not found.");

    public static readonly Error RemovedFromCode = Error.Conflict(
        ControlPlaneErrorCodes.FeatureFlagRemovedFromCode, "The code no longer defines this flag, so its rules no longer change.");

    private FeatureFlag(Guid id, string key, FeatureFlagType type, bool defaultValue, string description, DateTimeOffset addedAt, string? addedRevision)
    {
        Id = id;
        Key = key;
        Type = type;
        DefaultValue = defaultValue;
        Description = description;
        AddedAt = addedAt;
        AddedRevision = addedRevision;
        EveryoneElse = defaultValue;
        StateSince = addedAt;
        LastChangedAt = addedAt;
    }

    // Whom the rules reach: everyone (rule 3, or rule 2 at 100), nobody, or some organisations.
    private enum Reach
    {
        Some,
        Everyone,
        Nobody,
    }

    public Guid Id { get; private init; }

    public string Key { get; private init; }

    public FeatureFlagType Type { get; private init; }

    /// <summary>The value when no rule can be read: off for a release flag, on for a kill switch (12.05).</summary>
    public bool DefaultValue { get; private init; }

    /// <summary>What the flag does, as the code describes it at the latest deploy that defines it.</summary>
    public string Description { get; private set; }

    public DateTimeOffset AddedAt { get; private init; }

    /// <summary>The commit (<c>SOURCE_REVISION</c>) of the deploy that first stored the flag; none for a deploy that names none.</summary>
    public string? AddedRevision { get; private init; }

    /// <summary>When the first deploy whose code retires the flag archived it; none while the code defines it.</summary>
    public DateTimeOffset? RemovedAt { get; private set; }

    public string? RemovedRevision { get; private set; }

    /// <summary>Rule 1: how many organisations are listed, the count of the flag's <see cref="FeatureFlagTenant"/> rows.</summary>
    public int ListedCount { get; private set; }

    /// <summary>Rule 2: the percentage of the other organisations, from 0 to 100; 0 turns the rule off.</summary>
    public int Percentage { get; private set; }

    /// <summary>Rule 3: whether the flag is on for everyone else. A new flag starts with its default value.</summary>
    public bool EveryoneElse { get; private set; }

    /// <summary>
    /// Since when the flag is on for everyone, off for every organisation, or neither: the moment its rules last moved it from one of the
    /// three to another. Its cleanup counts from it (12.19: "Off everywhere for 34 days").
    /// </summary>
    public DateTimeOffset StateSince { get; private set; }

    /// <summary>The moment of the flag's latest change: its addition, its removal from the code or a rule's change.</summary>
    public DateTimeOffset LastChangedAt { get; private set; }

    /// <summary>The user id of the system admin of the latest change; none when a deploy made it.</summary>
    public Guid? LastChangedBy { get; private set; }

    public bool IsRemovedFromCode => RemovedAt is not null;

    private Reach State => EveryoneElse || Percentage == MaxPercentage ? Reach.Everyone : ListedCount == 0 && Percentage == 0 ? Reach.Nobody : Reach.Some;

    /// <summary>A flag the code defines and the environment does not store yet, stored by the deploy at this moment, with its rules at their start.</summary>
    public static (FeatureFlag Flag, FeatureFlagChange Change) Add(FeatureFlagDefinition definition, DateTimeOffset now, string? revision)
    {
        var flag = new FeatureFlag(Guid.CreateVersion7(now), definition.Key, definition.Type, definition.DefaultValue, definition.Description, now, revision);
        return (flag, FeatureFlagChange.OfDeploy(flag.Id, FeatureFlagChangeKind.AddedToCode, now, revision));
    }

    /// <summary>The code describes the flag anew; a description it already has changes nothing.</summary>
    public StateChange Describe(string description)
    {
        if (Description == description)
        {
            return StateChange.Unchanged;
        }

        Description = description;
        return StateChange.Changed;
    }

    /// <summary>
    /// The deploy whose code retires the flag archives it at this moment. An archived flag stays archived, with the moment and the commit
    /// of the first such deploy, and its rules stay as they were.
    /// </summary>
    public FeatureFlagChange? RemoveFromCode(DateTimeOffset now, string? revision)
    {
        if (IsRemovedFromCode)
        {
            return null;
        }

        RemovedAt = now;
        RemovedRevision = revision;
        LastChangedAt = now;
        LastChangedBy = null;
        return FeatureFlagChange.OfDeploy(Id, FeatureFlagChangeKind.RemovedFromCode, now, revision);
    }

    /// <summary>
    /// Rule 1 lists the organisation. An organisation listed already changes nothing, and answers none. Each method of a rule answers the
    /// change it made, which is the history's next line, or none when it changed nothing (I1). An archived flag is refused first.
    /// </summary>
    public Result<FeatureFlagChange?> AddTenant(Guid tenantId, bool listed, Guid actor, DateTimeOffset now)
    {
        if (IsRemovedFromCode)
        {
            return RemovedFromCode;
        }

        if (listed)
        {
            return (FeatureFlagChange?)null;
        }

        return Change(() => ListedCount++, FeatureFlagChangeKind.Listed, null, null, tenantId, actor, now);
    }

    /// <summary>Rule 1 no longer lists the organisation. An organisation that is not listed changes nothing.</summary>
    public Result<FeatureFlagChange?> RemoveTenant(Guid tenantId, bool listed, Guid actor, DateTimeOffset now)
    {
        if (IsRemovedFromCode)
        {
            return RemovedFromCode;
        }

        if (!listed)
        {
            return (FeatureFlagChange?)null;
        }

        return Change(() => ListedCount--, FeatureFlagChangeKind.Unlisted, null, null, tenantId, actor, now);
    }

    /// <summary>Rule 2 takes the percentage, a whole number from 0 to 100. The percentage it has changes nothing.</summary>
    public Result<FeatureFlagChange?> SetPercentage(int percentage, Guid actor, DateTimeOffset now)
    {
        if (IsRemovedFromCode)
        {
            return RemovedFromCode;
        }

        if (percentage is < 0 or > MaxPercentage)
        {
            return Error.Validation(ControlPlaneErrorCodes.FeatureFlagPercentageInvalid, $"A percentage is a whole number from 0 to {MaxPercentage}.");
        }

        if (percentage == Percentage)
        {
            return (FeatureFlagChange?)null;
        }

        var from = Percentage;
        return Change(
            () => Percentage = percentage,
            FeatureFlagChangeKind.PercentageChanged,
            from.ToString(CultureInfo.InvariantCulture),
            percentage.ToString(CultureInfo.InvariantCulture),
            null,
            actor,
            now);
    }

    /// <summary>Rule 3 turns the flag on or off for everyone else. The value it has changes nothing.</summary>
    public Result<FeatureFlagChange?> SetEveryoneElse(bool on, Guid actor, DateTimeOffset now)
    {
        if (IsRemovedFromCode)
        {
            return RemovedFromCode;
        }

        if (on == EveryoneElse)
        {
            return (FeatureFlagChange?)null;
        }

        return Change(() => EveryoneElse = on, FeatureFlagChangeKind.EveryoneElseChanged, OnOff(!on), OnOff(on), null, actor, now);
    }

    /// <summary>
    /// The organisation's purge takes it off rule 1 at this moment (docs/flows/tenant-purge.md). No system admin changed the rule, so the
    /// history gets no line and the latest change stays; the platform's log has the purge's entry.
    /// </summary>
    public void ForgetTenant(DateTimeOffset now) => Follow(() => ListedCount--, now);

    /// <summary>
    /// The flag's cleanup at this moment, or none: a release flag on for everyone since <see cref="CleanupAfter"/> is ready to remove, and
    /// any flag off for every organisation since then is unused. An archived flag has none: it left the code already.
    /// </summary>
    public FeatureFlagCleanup? CleanupAt(DateTimeOffset now) =>
        IsRemovedFromCode || now - StateSince < CleanupAfter ? null
        : State == Reach.Everyone && Type == FeatureFlagType.Release ? FeatureFlagCleanup.ReadyToRemove
        : State == Reach.Nobody ? FeatureFlagCleanup.Unused
        : null;

    /// <summary>The flag's rules as they apply to one organisation, which rule 1 lists or not.</summary>
    public FeatureFlagRules RulesFor(bool listed) => new(Key, listed, Percentage, EveryoneElse);

    private static string OnOff(bool on) => on ? "on" : "off";

    // A change that moves the flag to another reach starts its count of days again.
    private void Follow(Action change, DateTimeOffset now)
    {
        var before = State;
        change();
        if (State != before)
        {
            StateSince = now;
        }
    }

    private FeatureFlagChange Change(Action change, FeatureFlagChangeKind kind, string? from, string? to, Guid? tenantId, Guid actor, DateTimeOffset now)
    {
        Follow(change, now);
        LastChangedAt = now;
        LastChangedBy = actor;
        return FeatureFlagChange.OfRule(Id, kind, from, to, tenantId, now, actor);
    }
}
