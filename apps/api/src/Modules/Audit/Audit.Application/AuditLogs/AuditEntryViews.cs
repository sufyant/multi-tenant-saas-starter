using Audit.Application.Members;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Users;
using SharedKernel;

namespace Audit.Application.AuditLogs;

// API composition (Richardson, "API composition"): the entries come from the audit log, and the names of the people they point to from
// ControlPlane, in one call for the whole page. They are two queries: Wolverine gives a handler the transaction of one DbContext, and
// each of the two modules has its own (W1).
internal static class AuditEntryViews
{
    /// <summary>The page of the entries found, with no user named yet.</summary>
    public static CursorPage<AuditEntryView> PageOf(IReadOnlyList<AuditEntry> found, CursorRequest request) =>
        CursorPage.Of(found, request, entry => new CursorPosition(entry.OccurredAt, entry.Id))
            .Map(entry => new AuditEntryView(
                entry.Id,
                entry.OccurredAt,
                entry.Operation,
                AuditDetailsTypes.Read(entry.Operation, entry.Details),
                ActorOf(entry),
                TargetOf(entry)));

    /// <summary>The page with each user, actor, target or the user the details point to, named by ControlPlane now.</summary>
    public static async Task<CursorPage<AuditEntryView>> NamedAsync(
        CursorPage<AuditEntryView> page,
        IUserDirectory users,
        CancellationToken cancellationToken)
    {
        var people = page.Items
            .SelectMany(entry => new[]
            {
                entry.Actor.UserId,
                entry.Target is { Kind: nameof(AuditTargetKind.User) } target ? target.Id : null,
                UserOf(entry.Details),
            })
            .OfType<Guid>()
            .ToHashSet();
        var names = await users.FindNamesAsync(people, cancellationToken);

        return page.Map(entry => entry with
        {
            Actor = entry.Actor.UserId is { } actor ? entry.Actor with { DisplayName = NameOf(actor, names), Deleted = IsDeleted(actor, names) } : entry.Actor,
            Target = entry.Target is { Kind: nameof(AuditTargetKind.User) } target
                ? target with { Name = NameOf(target.Id, names), Deleted = IsDeleted(target.Id, names) }
                : entry.Target,
            DetailsUserName = UserOf(entry.Details) is { } user ? NameOf(user, names) : null,
            DetailsUserDeleted = UserOf(entry.Details) is { } named ? IsDeleted(named, names) : null,
        });
    }

    // The details that point to a user keep only the user's id (K3); the new Owner of tenant.owner_made is the one today.
    private static Guid? UserOf(AuditDetails? details) => details is TenantOwnerMadeDetails made ? made.UserId : null;

    // The platform has no name here: a screen names it from its kind, in its own language.
    private static AuditActorView ActorOf(AuditEntry entry) => entry.ActorKind switch
    {
        AuditActorKind.User => new(nameof(AuditActorKind.User), entry.ActorId, null, false),
        _ => new(nameof(AuditActorKind.Platform), null, null, null),
    };

    // A tenant keeps its stored name; a user is named later; an invitation has no name.
    private static AuditTargetView TargetOf(AuditEntry entry) =>
        new(entry.TargetKind.ToString(), entry.TargetId, entry.TargetName, entry.TargetKind == AuditTargetKind.User ? false : null);

    // An unknown id has no name, and is not known to be deleted.
    private static string? NameOf(Guid userId, IReadOnlyDictionary<Guid, UserName> names) =>
        names.TryGetValue(userId, out var name) ? name.DisplayName : null;

    private static bool IsDeleted(Guid userId, IReadOnlyDictionary<Guid, UserName> names) =>
        names.TryGetValue(userId, out var name) && name.Deleted;
}
