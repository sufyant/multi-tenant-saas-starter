using System.Linq.Expressions;
using ControlPlane.Domain.EmailAddresses;
using SharedKernel;

namespace ControlPlane.Domain.Users;

/// <summary>
/// One identity across tenants, matched to the identity provider's user id. ControlPlane owns the email address and the name (A7):
/// the identity provider keeps no name, and only the user or a system admin changes it.
/// </summary>
internal sealed class User(Guid id, string externalId)
{
    public const int NameMaxLength = 100;

    /// <summary>The longest reason of a suspension, as a tenant's (K4).</summary>
    public const int SuspensionReasonMaxLength = 500;

    /// <summary>
    /// The longest time zone name ControlPlane keeps. The longest IANA name today has 32 characters, and the IANA rules allow 14 for each
    /// part of a name.
    /// </summary>
    public const int TimeZoneMaxLength = 64;

    // Static members are initialized in the order of the text, so the expression comes before its compiled form.
    private static readonly Expression<Func<User, UserStatus>> StatusExpression = user =>
        user.DeletedAt != null ? UserStatus.Deleted : user.SuspendedAt != null ? UserStatus.Suspended : UserStatus.Active;

    private static readonly Func<User, UserStatus> CompiledStatusOf = StatusExpression.Compile();

    /// <summary>
    /// The user's status: a deletion outranks a suspension, because the identity provider no longer knows the person. It is an
    /// expression, so the queries filter by the same rule (N7s).
    /// </summary>
    public static Expression<Func<User, UserStatus>> StatusOf => StatusExpression;

    public Guid Id { get; private init; } = id;

    public string ExternalId { get; private init; } = externalId;

    /// <summary>The user's address, in its one form (<see cref="EmailAddress"/>); none once the user is deleted.</summary>
    public string? Email { get; private set; }

    /// <summary>The person's whole name, as they write it: one field, never split into parts (A7); none once the user is deleted.</summary>
    public string? Name { get; private set; }

    /// <summary>
    /// The language the web speaks to the user. A new user starts with their invitation's; a root system admin whom the system door lets in
    /// first starts with English.
    /// </summary>
    public Language Language { get; private set; }

    /// <summary>The IANA name of the user's time zone, such as <c>Europe/Dublin</c>; null when it is not set.</summary>
    public string? TimeZone { get; private set; }

    /// <summary>When a system admin deleted the user. The row stays: audit entries point to it.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>When a system admin suspended the user. A suspended user is refused on every me and tenant endpoint (A9).</summary>
    public DateTimeOffset? SuspendedAt { get; private set; }

    /// <summary>The user id of the system admin who suspended the user, while the user is suspended.</summary>
    public Guid? SuspendedBy { get; private set; }

    /// <summary>
    /// Why a system admin suspended the user, in their words, while the user is suspended; only the platform sees it. It is personal data
    /// about the user: the user's data export holds it and the deletion erases it, and no audit entry keeps it, because the platform's log
    /// stays after the deletion (A8; the maintainer's decision of 2026-11-08). None for a suspension before the column.
    /// </summary>
    public string? SuspensionReason { get; private set; }

    /// <summary>
    /// When the person became a user: when they accepted their first invitation, or when the system door let them in as a root system admin.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>
    /// How many tenants the user is a member of. The memberships are under row level security, and the platform's user list reads
    /// across tenants, so the user row keeps the count (R13). A command that adds or removes a membership changes it in its own
    /// transaction; the database adds one or takes one away, so two tenants' commands for one user never lose a change.
    /// </summary>
    public int MembershipCount { get; private init; }

    public UserStatus Status => CompiledStatusOf(this);

    /// <summary>The users in a status, as a query reads them: <see cref="StatusOf"/> compared with the status.</summary>
    public static Expression<Func<User, bool>> InStatus(UserStatus status) =>
        Expression.Lambda<Func<User, bool>>(Expression.Equal(StatusOf.Body, Expression.Constant(status)), StatusOf.Parameters);

    /// <summary>The users in any of the statuses, as a query reads them: <see cref="InStatus"/> of each, joined by "or".</summary>
    public static Expression<Func<User, bool>> InAnyStatus(IReadOnlyList<UserStatus> statuses) =>
        Expression.Lambda<Func<User, bool>>(
            statuses
                .Select(status => (Expression)Expression.Equal(StatusOf.Body, Expression.Constant(status)))
                .Aggregate(Expression.OrElse),
            StatusOf.Parameters);

    /// <summary>
    /// A person who is not a user yet joins through an invitation, at this moment: the invited address, which the identity provider
    /// verified, and the name they typed, without the spaces around it. They have no membership yet: ControlPlane adds the membership of
    /// the tenant they join, and counts it as it counts every membership (R13).
    /// </summary>
    public static User Join(Guid id, string externalId, EmailAddress email, string name, Language language, DateTimeOffset now) =>
        new(id, externalId)
        {
            Email = email.Value,
            Name = name.Trim(),
            Language = language,
            CreatedAt = now,
        };

    /// <summary>
    /// A root system admin who is not a user yet enters through the system door, at this moment: the listed address, which the identity
    /// provider verified, no name, as the identity provider keeps none, and English, as no invitation chose a language. A system admin
    /// gives them their name (A7).
    /// </summary>
    public static User EnterAsRootSystemAdmin(Guid id, string externalId, EmailAddress email, DateTimeOffset now) =>
        new(id, externalId)
        {
            Email = email.Value,
            Language = Language.En,
            CreatedAt = now,
        };

    /// <summary>
    /// The user changes their own preferences, which follow them to every tenant and device. Changed when they changed. The API keeps
    /// the IANA name the web chose, and null means the web uses the browser's. Only the purge warning reads the zone (docs/flows/language-and-time-zone.md). A deleted user is
    /// refused, as on every me endpoint (A9).
    /// </summary>
    public Result<StateChange> ChangePreferences(Language language, string? timeZone)
    {
        if (DeletedAt is not null)
        {
            return Error.Forbidden(ControlPlaneErrorCodes.UserDeleted, "Your account is deleted.");
        }

        var changed = (Language, TimeZone) != (language, timeZone);
        (Language, TimeZone) = (language, timeZone);
        return changed ? StateChange.Changed : StateChange.Unchanged;
    }

    /// <summary>
    /// A system admin changes the name. Changed when it changed; the same name with spaces around it is no change. A deleted user has
    /// no name to change: giving them one would keep personal data the deletion erased.
    /// </summary>
    public Result<StateChange> Rename(string name) =>
        DeletedAt is null ? SetName(name) : Error.Conflict(ControlPlaneErrorCodes.UserDeleted, "The user is deleted.");

    /// <summary>
    /// The user corrects their own name, under the rules of <see cref="Rename"/>. A deleted user is refused, as on every me endpoint
    /// (A9).
    /// </summary>
    public Result<StateChange> RenameMe(string name) =>
        DeletedAt is null ? SetName(name) : Error.Forbidden(ControlPlaneErrorCodes.UserDeleted, "Your account is deleted.");

    /// <summary>
    /// The user changes their own address to one whose code they gave (A7): ControlPlane owns the address, and the identity provider takes
    /// it after the commit. Changed when it changed. A deleted user is refused, as on every me endpoint (A9).
    /// </summary>
    public Result<StateChange> ChangeEmail(EmailAddress email)
    {
        if (DeletedAt is not null)
        {
            return Error.Forbidden(ControlPlaneErrorCodes.UserDeleted, "Your account is deleted.");
        }

        var changed = Email != email.Value;
        Email = email.Value;
        return changed ? StateChange.Changed : StateChange.Unchanged;
    }

    /// <summary>
    /// Erases the email address, the name, the time zone, which tells where the person lives, and the reason of a suspension, and marks
    /// the user deleted. A second deletion keeps the first time. Changed for the first deletion.
    /// </summary>
    public StateChange Delete(DateTimeOffset now)
    {
        var first = DeletedAt is null;
        (Email, Name, TimeZone, SuspensionReason) = (null, null, null, null);
        DeletedAt ??= now;
        return first ? StateChange.Changed : StateChange.Unchanged;
    }

    /// <summary>
    /// A system admin suspends the user for the reason, kept without the spaces around it. A second suspension keeps the first time, the
    /// first system admin and the first reason. The identity provider is not told. Changed when the user was not suspended. The caller
    /// checked the reason's length. A deleted user is refused: the reason would keep personal data the deletion erased.
    /// </summary>
    public Result<StateChange> Suspend(DateTimeOffset now, Guid suspendedBy, string reason)
    {
        if (DeletedAt is not null)
        {
            return Error.Conflict(ControlPlaneErrorCodes.UserDeleted, "The user is deleted.");
        }

        if (SuspendedAt is not null)
        {
            return StateChange.Unchanged;
        }

        (SuspendedAt, SuspendedBy, SuspensionReason) = (now, suspendedBy, reason.Trim());
        return StateChange.Changed;
    }

    /// <summary>Reactivates the user and forgets who suspended them and why. Changed when the user was suspended.</summary>
    public StateChange Reactivate()
    {
        var changed = SuspendedAt is not null;
        (SuspendedAt, SuspendedBy, SuspensionReason) = (null, null, null);
        return changed ? StateChange.Changed : StateChange.Unchanged;
    }

    // The name without the spaces around it. Changed when it changed.
    private StateChange SetName(string name)
    {
        var changed = Name != name.Trim();
        Name = name.Trim();
        return changed ? StateChange.Changed : StateChange.Unchanged;
    }
}
