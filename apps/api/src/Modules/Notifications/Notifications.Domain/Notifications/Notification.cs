using SharedKernel;

namespace Notifications.Domain.Notifications;

/// <summary>
/// One notification of one user (U3): its type, the tenant it is about or none, the values its texts need, when it came and when the user
/// opened it. A value keeps a person by user id only, and the read names them (U2); any other value is kept as it was at that moment. The texts are not stored: the read renders them in the reader's language (U2).
/// </summary>
internal sealed class Notification
{
    // For EF Core, which reads a notification back.
    private Notification()
    {
        Type = null!;
        Values = null!;
    }

    private Notification(Guid id, Guid userId, Guid? tenantId, NotificationKind kind, IReadOnlyDictionary<string, string> values, DateTimeOffset createdAt)
    {
        (Id, UserId, TenantId, Type, Bell, Values, CreatedAt) = (id, userId, tenantId, kind.Type, kind.Bell, new(values), createdAt);
    }

    public Guid Id { get; private init; }

    public Guid UserId { get; private init; }

    /// <summary>The tenant it is about; none for a personal notification.</summary>
    public Guid? TenantId { get; private init; }

    public string Type { get; private init; }

    public NotificationBell Bell { get; private init; }

    public Dictionary<string, string> Values { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>
    /// The notification of a type of the catalog, with exactly the values its texts use; an optional value may be missing. A tenant notification is about a tenant, and a
    /// personal one about none: anything else is a mistake of ours.
    /// </summary>
    public static Notification Create(
        Guid id,
        Guid userId,
        Guid? tenantId,
        string type,
        IReadOnlyDictionary<string, string> values,
        DateTimeOffset createdAt)
    {
        var kind = NotificationCatalog.Of(type);
        var expected = kind.Values
            .Where(value => !value.Optional || values.ContainsKey(value.Name))
            .SelectMany(NamesOf)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (!expected.SequenceEqual(values.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"A notification of type {type} needs exactly the values {string.Join(", ", expected)}.");
        }

        if ((kind.Bell == NotificationBell.Tenant && tenantId is null) || (kind.Bell == NotificationBell.Personal && tenantId is not null))
        {
            throw new InvalidOperationException($"A notification of type {type} for the {kind.Bell} bell has the wrong tenant.");
        }

        return new Notification(id, userId, tenantId, kind, values, createdAt);
    }

    /// <summary>The names a value stores: a role stores whether it is built in beside its name.</summary>
    public static IEnumerable<string> NamesOf(NotificationValue value) =>
        value.Kind == NotificationValueKind.Role ? [value.Name, $"{value.Name}BuiltIn"] : [value.Name];

    /// <summary>The user opened it. Opening it again changes nothing.</summary>
    public StateChange MarkRead(DateTimeOffset now)
    {
        if (ReadAt is not null)
        {
            return StateChange.Unchanged;
        }

        ReadAt = now;
        return StateChange.Changed;
    }
}
