using Audit.Application.Ports;
using Audit.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Audit.Infrastructure.Persistence;

/// <summary>The audit logs on the module's DbContext, in the transaction Wolverine began for the handler.</summary>
/// <remarks>Public only so that Wolverine's generated code can build it on the handler's own DbContext (W1, W9).</remarks>
public sealed class AuditLog(AuditDbContext audit) : IAuditLog
{
    async Task IAuditLog.RecordAsync(PlatformAuditEntry? platform, AuditEntry? tenant, CancellationToken cancellationToken)
    {
        var stored = tenant is not null
            ? await audit.Entries.AnyAsync(entry => entry.Id == tenant.Id, cancellationToken)
            : await audit.PlatformEntries.AnyAsync(entry => entry.Id == platform!.Id, cancellationToken);
        if (stored)
        {
            return;
        }

        if (platform is not null)
        {
            audit.PlatformEntries.Add(platform);
        }

        if (tenant is not null)
        {
            audit.Entries.Add(tenant);
        }

        await audit.SaveChangesAsync(cancellationToken);
    }

    async Task<IReadOnlyList<AuditRecord>> IAuditLog.ReadTenantLogAsync(CursorRequest page, CancellationToken cancellationToken) =>
        await PageOf(audit.Entries, page).ToListAsync(cancellationToken);

    // An entry about a tenant has the tenant as its target. The index on the target and the order serves the filter (K5).
    async Task<IReadOnlyList<AuditRecord>> IAuditLog.ReadPlatformLogAsync(CursorRequest page, Guid? tenantId, CancellationToken cancellationToken) =>
        await PageOf(
                tenantId is { } tenant
                    ? audit.PlatformEntries.Where(entry => entry.TargetKind == AuditTargetKind.Tenant && entry.TargetId == tenant)
                    : audit.PlatformEntries,
                page)
            .ToListAsync(cancellationToken);

    // Newest first, by the moment and then the id. The cursor's place is compared as one row value, (occurred_at, id) < (at, id), which
    // the index on the same columns serves (K5).
    private static IQueryable<AuditRecord> PageOf<TRecord>(IQueryable<TRecord> log, CursorRequest page)
        where TRecord : AuditRecord
    {
        var after = page.After is { } place
            ? log.Where(record => EF.Functions.LessThan(ValueTuple.Create(record.OccurredAt, record.Id), ValueTuple.Create(place.At, place.Id)))
            : log;

        return after
            .OrderByDescending(record => record.OccurredAt)
            .ThenByDescending(record => record.Id)
            .Take(page.Limit + 1)
            .AsNoTracking();
    }
}
