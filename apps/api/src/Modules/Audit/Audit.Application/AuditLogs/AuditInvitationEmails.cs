using Audit.Application.Invitations;
using Audit.Domain.AuditEntries;
using SharedKernel;

namespace Audit.Application.AuditLogs;

// K3: an entry keeps the id of the invitation it points to, never its address. The read takes the address from the invitation row now,
// as it takes a person's name from the user row, so a user's deletion, which erases the address there (A8), takes it out of the log too.
// The invitations are a tenant's rows (R1): the tenant's log reads them in its own tenant, and the platform's log, whose transaction
// declares no tenant (R11), in the tenant of each entry, with one query for each tenant on the page.
public static class AuditInvitationEmails
{
    /// <summary>The invitations the page points to.</summary>
    public static IReadOnlyCollection<Guid> Of(IEnumerable<AuditEntryView> entries) =>
        entries.Select(InvitationOf).OfType<Guid>().ToHashSet();

    /// <summary>The invitations the page points to, by the tenant each entry is about; an entry that names no tenant is left out.</summary>
    public static IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> ByTenant(IEnumerable<AuditEntryView> entries) =>
        entries
            .Where(entry => InvitationOf(entry) is not null && entry.Target.Kind == nameof(AuditTargetKind.Tenant))
            .GroupBy(entry => entry.Target.Id)
            .ToDictionary(tenant => tenant.Key, tenant => Of(tenant));

    /// <summary>The page with the address of each invitation found; an invitation without one names nobody.</summary>
    public static CursorPage<AuditEntryView> With(CursorPage<AuditEntryView> page, IReadOnlyDictionary<Guid, string> emails) =>
        page.Map(entry => InvitationOf(entry) is { } invitation && emails.TryGetValue(invitation, out var email) ? entry with { InvitationEmail = email } : entry);

    /// <summary>The address of an invitation target; null for a target of another kind.</summary>
    public static string? OfTarget(AuditEntryView entry) =>
        entry.Target.Kind == nameof(AuditTargetKind.Invitation) ? entry.InvitationEmail : null;

    // member.invited and invitation.revoked point to the invitation as their target; tenant.owner_invited keeps it in its details.
    private static Guid? InvitationOf(AuditEntryView entry) => entry switch
    {
        { Details: TenantOwnerInvitedDetails invited } => invited.InvitationId,
        { Target.Kind: nameof(AuditTargetKind.Invitation) } => entry.Target.Id,
        _ => null,
    };
}
