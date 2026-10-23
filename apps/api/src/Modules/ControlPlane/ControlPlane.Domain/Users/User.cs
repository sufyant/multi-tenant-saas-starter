using System.Linq.Expressions;
using ControlPlane.Domain.EmailAddresses;
using SharedKernel;

namespace ControlPlane.Domain.Users;

/// <summary>
/// One identity across tenants, matched to the identity provider's user id. ControlPlane owns the email address and the names (A7):
/// the identity provider keeps no name, and only the user or a system admin changes one.
/// </summary>
internal sealed class User(Guid id, string externalId)
{
    public const int NameMaxLength = 100;

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

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

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

    /// <summary>
    /// A person who is not a user yet joins through an invitation, at this moment: the invited address, which the identity provider
    /// verified, and the names they typed, without the spaces around them. They have no membership yet: ControlPlane adds the membership of
    /// the tenant they join, and counts it as it counts every membership (R13).
    /// </summary>
    public static User Join(Guid id, string externalId, EmailAddress email, string firstName, string lastName, Language language, DateTimeOffset now) =>
        new(id, externalId)
        {
            Email = email.Value,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Language = language,
            CreatedAt = now,
        };

    /// <summary>
    /// A root system admin who is not a user yet enters through the system door, at this moment: the listed address, which the identity
    /// provider verified, no names, as the identity provider keeps none, and English, as no invitation chose a language. A system admin
    /// gives them their names (A7).
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
    /// the IANA name the web chose, and null means the web uses the browser's. Only the purge warning reads the zone (section 6). A deleted user is
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
    /// A system admin changes the names. Changed when they changed; the same names with spaces around them are no change. A deleted user
    /// has no names to change: giving them one would keep personal data the deletion erased.
    /// </summary>
    public Result<StateChange> Rename(string firstName, string lastName) =>
        DeletedAt is null ? SetNames(firstName, lastName) : Error.Conflict(ControlPlaneErrorCodes.UserDeleted, "The user is deleted.");

    /// <summary>
    /// The user corrects their own names, under the rules of <see cref="Rename"/>. A deleted user is refused, as on every me endpoint
    /// (A9).
    /// </summary>
    public Result<StateChange> RenameMe(string firstName, string lastName) =>
        DeletedAt is null ? SetNames(firstName, lastName) : Error.Forbidden(ControlPlaneErrorCodes.UserDeleted, "Your account is deleted.");

    /// <summary>
    /// Erases the email address, the names and the time zone, which tells where the person lives, and marks the user deleted. A second
    /// deletion keeps the first time. Changed for the first deletion.
    /// </summary>
    public StateChange Delete(DateTimeOffset now)
    {
        var first = DeletedAt is null;
        (Email, FirstName, LastName, TimeZone) = (null, null, null, null);
        DeletedAt ??= now;
        return first ? StateChange.Changed : StateChange.Unchanged;
    }

    /// <summary>
    /// A system admin suspends the user. A second suspension keeps the first time and the first system admin. The identity provider is
    /// not told. Changed when the user was not suspended.
    /// </summary>
    public StateChange Suspend(DateTimeOffset now, Guid suspendedBy)
    {
        if (SuspendedAt is not null)
        {
            return StateChange.Unchanged;
        }

        (SuspendedAt, SuspendedBy) = (now, suspendedBy);
        return StateChange.Changed;
    }

    /// <summary>Reactivates the user and forgets who suspended them. Changed when the user was suspended.</summary>
    public StateChange Reactivate()
    {
        var changed = SuspendedAt is not null;
        (SuspendedAt, SuspendedBy) = (null, null);
        return changed ? StateChange.Changed : StateChange.Unchanged;
    }

    // The names without the spaces around them. Changed when they changed.
    private StateChange SetNames(string firstName, string lastName)
    {
        var changed = (FirstName, LastName) != (firstName.Trim(), lastName.Trim());
        (FirstName, LastName) = (firstName.Trim(), lastName.Trim());
        return changed ? StateChange.Changed : StateChange.Unchanged;
    }
}
