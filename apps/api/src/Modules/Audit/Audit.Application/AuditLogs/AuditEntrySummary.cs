using Audit.Domain;
using ControlPlane.Contracts;
using SharedKernel;

namespace Audit.Application.AuditLogs;

/// <summary>
/// One record of an audit log as a reader sees it: the stored record, with each person's name resolved now (K3). It holds no email
/// address. The kinds are the Domain's names (<c>User</c>, <c>Tenant</c>); the details are the stored JSON object, or null.
/// </summary>
public sealed record AuditEntrySummary(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Operation,
    string? Details,
    AuditActorSummary Actor,
    AuditTargetSummary? Target);

/// <summary>Who acted: a user carries their catalog user id; the name is null for an id the catalog does not know.</summary>
public sealed record AuditActorSummary(string Kind, Guid? UserId, string? DisplayName);

/// <summary>What the action was done to: a tenant's stored name, a user's resolved name, and no name for an invitation.</summary>
public sealed record AuditTargetSummary(string Kind, Guid Id, string? Name);

// API composition (Richardson, "API composition"): the records come from the audit log, and the names of the people they point to from
// ControlPlane, in one call for the whole page.
internal static class AuditEntrySummaries
{
    private const string Platform = "Platform";

    private const string IdentityProvider = "Identity provider";

    /// <summary>The page of the records found, with each user, actor or target, named by the catalog now.</summary>
    public static async Task<CursorPage<AuditEntrySummary>> PageOfAsync(
        IReadOnlyList<AuditRecord> found,
        CursorRequest request,
        IUserNames userNames,
        CancellationToken cancellationToken)
    {
        var page = CursorPage.Of(found, request, record => new CursorPosition(record.OccurredAt, record.Id))
            .Map(record => new AuditEntrySummary(record.Id, record.OccurredAt, record.Operation, record.Details, ActorOf(record), TargetOf(record)));
        var people = page.Items
            .SelectMany(entry => new[] { entry.Actor.UserId, entry.Target is { Kind: nameof(AuditTargetKind.User) } target ? target.Id : null })
            .OfType<Guid>()
            .ToHashSet();
        var names = await userNames.FindAsync(people, cancellationToken);

        return page.Map(entry => entry with
        {
            Actor = entry.Actor.UserId is { } actor ? entry.Actor with { DisplayName = NameOf(actor, names) } : entry.Actor,
            Target = entry.Target is { Kind: nameof(AuditTargetKind.User) } target ? target with { Name = NameOf(target.Id, names) } : entry.Target,
        });
    }

    // A record written before K3 has no actor kind: a system admin acted on the tenant, and the read side shows it as the platform's,
    // as a system admin's action in the tenant's log always is.
    private static AuditActorSummary ActorOf(AuditRecord record) => record.ActorKind switch
    {
        AuditActorKind.User => new(nameof(AuditActorKind.User), record.ActorId, null),
        AuditActorKind.IdentityProvider => new(nameof(AuditActorKind.IdentityProvider), null, IdentityProvider),
        _ => new(nameof(AuditActorKind.Platform), null, Platform),
    };

    // A record written before K3 has no target. A tenant keeps its stored name; a user is named later; an invitation has no name.
    private static AuditTargetSummary? TargetOf(AuditRecord record) =>
        record is { TargetKind: { } kind, TargetId: { } id } ? new AuditTargetSummary(kind.ToString(), id, record.TargetName) : null;

    // An id the catalog does not know has no name.
    private static string? NameOf(Guid userId, IReadOnlyDictionary<Guid, UserName> names) =>
        names.TryGetValue(userId, out var name) ? name.DisplayName : null;
}
