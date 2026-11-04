using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Audit.Infrastructure.Persistence.AuditLogs;

/// <summary>
/// Audit's part of a person's data export (docs/flows/personal-data-export.md): the actions the person did, in the platform's log and in
/// the log of every tenant, each by its operation, its moment, its tenant and its target. No details: they describe the organisation and its
/// other members. The platform's log is read in a transaction that declares nobody (platform_read, R11), and the tenants' logs in one that
/// declares the user, so the own_actions policy shows only the person's own entries, in every tenant (R11), and the transaction declares no
/// tenant (R4).
/// </summary>
/// <remarks>Public because Wolverine resolves it from the message's scope (W9).</remarks>
public sealed class AuditDataExporter(AuditDbContext audit) : IPersonalDataExporter
{
    public async Task<IReadOnlyList<PersonalDataPart>> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var platformRead = await audit.Database.BeginTransactionAsync(cancellationToken);
        var platform = await audit.PlatformEntries
            .AsNoTracking()
            .Where(entry => entry.ActorId == userId)
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .Select(entry => new ExportedAction(entry.OccurredAt, entry.Operation, null, entry.TargetKind, entry.TargetId))
            .ToListAsync(cancellationToken);
        await platformRead.CommitAsync(cancellationToken);

        await using var tenantRead = await audit.Database.BeginTransactionAsync(cancellationToken);
        await audit.DeclareUserAsync(userId, cancellationToken);
        var tenants = await audit.Entries
            .AsNoTracking()
            .Where(entry => entry.ActorId == userId)
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .Select(entry => new ExportedAction(entry.OccurredAt, entry.Operation, EF.Property<Guid>(entry, "TenantId"), entry.TargetKind, entry.TargetId))
            .ToListAsync(cancellationToken);
        await tenantRead.CommitAsync(cancellationToken);

        return
        [
            new PersonalDataPart(
                "activity",
                platform.Concat(tenants)
                    .OrderBy(action => action.OccurredAt)
                    .Select(action => new { action.OccurredAt, action.Operation, action.TenantId, TargetKind = action.TargetKind.ToString(), action.TargetId })
                    .ToList()),
        ];
    }

    private sealed record ExportedAction(DateTimeOffset OccurredAt, string Operation, Guid? TenantId, Domain.AuditEntries.AuditTargetKind TargetKind, Guid TargetId);
}
