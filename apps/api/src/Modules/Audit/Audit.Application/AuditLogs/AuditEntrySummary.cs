using Audit.Domain;
using ControlPlane.Contracts;
using SharedKernel;

namespace Audit.Application.AuditLogs;

/// <summary>
/// One record of an audit log as a reader sees it: the stored record, with each person's name resolved now (K3). It holds no email
/// address. The kinds are the Domain's names (<c>User</c>, <c>Tenant</c>); the details are the stored JSON object, or null.
/// </summary>
public sealed record AuditEntryView(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Operation,
    string? Details,
    AuditActorView Actor,
    AuditTargetView Target);

/// <summary>
/// Who acted: a user carries their catalog user id, their real name or null, and whether they were deleted. The platform carries only
/// its kind.
/// </summary>
public sealed record AuditActorView(string Kind, Guid? UserId, string? DisplayName, bool? Deleted);

/// <summary>
/// What the action was done to: a tenant with its stored name, a user with their real name or null and whether they were deleted, or an
/// invitation, which has no name.
/// </summary>
public sealed record AuditTargetView(string Kind, Guid Id, string? Name, bool? Deleted);

// API composition (Richardson, "API composition"): the records come from the audit log, and the names of the people they point to from
// ControlPlane, in one call for the whole page. They are two queries: Wolverine gives a handler the transaction of one DbContext, and
// each of the two modules has its own (W1).
internal static class AuditEntryViews
{
    /// <summary>The page of the records found, with no user named yet.</summary>
    public static CursorPage<AuditEntryView> PageOf(IReadOnlyList<AuditRecord> found, CursorRequest request) =>
        CursorPage.Of(found, request, record => new CursorPosition(record.OccurredAt, record.Id))
            .Map(record => new AuditEntryView(record.Id, record.OccurredAt, record.Operation, record.Details, ActorOf(record), TargetOf(record)));

    /// <summary>The page with each user, actor or target, named by the catalog now.</summary>
    public static async Task<CursorPage<AuditEntryView>> NamedAsync(
        CursorPage<AuditEntryView> page,
        IUserDirectory users,
        CancellationToken cancellationToken)
    {
        var people = page.Items
            .SelectMany(entry => new[] { entry.Actor.UserId, entry.Target is { Kind: nameof(AuditTargetKind.User) } target ? target.Id : null })
            .OfType<Guid>()
            .ToHashSet();
        var names = await users.FindNamesAsync(people, cancellationToken);

        return page.Map(entry => entry with
        {
            Actor = entry.Actor.UserId is { } actor ? entry.Actor with { DisplayName = NameOf(actor, names), Deleted = IsDeleted(actor, names) } : entry.Actor,
            Target = entry.Target is { Kind: nameof(AuditTargetKind.User) } target
                ? target with { Name = NameOf(target.Id, names), Deleted = IsDeleted(target.Id, names) }
                : entry.Target,
        });
    }

    // The platform has no name here: a screen names it from its kind, in its own language.
    private static AuditActorView ActorOf(AuditRecord record) => record.ActorKind switch
    {
        AuditActorKind.User => new(nameof(AuditActorKind.User), record.ActorId, null, false),
        _ => new(nameof(AuditActorKind.Platform), null, null, null),
    };

    // A tenant keeps its stored name; a user is named later; an invitation has no name.
    private static AuditTargetView TargetOf(AuditRecord record) =>
        new(record.TargetKind.ToString(), record.TargetId, record.TargetName, record.TargetKind == AuditTargetKind.User ? false : null);

    // An id the catalog does not know has no name, and is not known to be deleted.
    private static string? NameOf(Guid userId, IReadOnlyDictionary<Guid, UserName> names) =>
        names.TryGetValue(userId, out var name) ? name.DisplayName : null;

    private static bool IsDeleted(Guid userId, IReadOnlyDictionary<Guid, UserName> names) =>
        names.TryGetValue(userId, out var name) && name.Deleted;
}
