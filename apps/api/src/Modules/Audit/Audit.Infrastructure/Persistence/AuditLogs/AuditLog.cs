using Audit.Application.AuditLogs;
using Audit.Domain.AuditEntries;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Audit.Infrastructure.Persistence.AuditLogs;

/// <summary>The audit logs on the module's DbContext, in the transaction Wolverine began for the handler.</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class AuditLog(AuditDbContext audit) : IAuditLog
{
    async Task IAuditLog.RecordAsync(PlatformAuditEntry? platform, TenantAuditEntry? tenant, CancellationToken cancellationToken)
    {
        // An event that arrives again has the same moment and id, so the lookup reads the key (R18).
        var stored = tenant is not null
            ? await audit.Entries.AnyAsync(entry => entry.OccurredAt == tenant.OccurredAt && entry.Id == tenant.Id, cancellationToken)
            : await audit.PlatformEntries.AnyAsync(entry => entry.OccurredAt == platform!.OccurredAt && entry.Id == platform.Id, cancellationToken);
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

    // R14: the application may only execute the function, which deletes the entries of the tenant the transaction declares.
    async Task IAuditLog.PurgeTenantEntriesAsync(CancellationToken cancellationToken)
    {
        _ = audit.MessageTenantId ?? throw new InvalidOperationException("A tenant's log is purged inside the tenant.");
        await audit.Database.ExecuteSqlAsync($"SELECT audit.purge_tenant_entries()", cancellationToken);
    }

    async Task<IReadOnlyList<AuditEntry>> IAuditLog.ListTenantEntriesAsync(CursorRequest page, CancellationToken cancellationToken) =>
        await PageOf(audit.Entries, page).ToListAsync(cancellationToken);

    // An entry about a tenant has the tenant as its target. The index on the target and the order serves the filter (K5).
    async Task<IReadOnlyList<AuditEntry>> IAuditLog.ListPlatformEntriesAsync(CursorRequest page, Guid? tenantId, CancellationToken cancellationToken) =>
        await PageOf(
                tenantId is { } tenant
                    ? audit.PlatformEntries.Where(entry => entry.TargetKind == AuditTargetKind.Tenant && entry.TargetId == tenant)
                    : audit.PlatformEntries,
                page)
            .ToListAsync(cancellationToken);

    // Newest first, by the moment and then the id. The cursor's place is compared as one row value, (occurred_at, id) < (at, id), which
    // the index on the same columns serves (K5).
    private static IQueryable<AuditEntry> PageOf<TEntry>(IQueryable<TEntry> log, CursorRequest page)
        where TEntry : AuditEntry
    {
        var after = page.After is { } place
            ? log.Where(entry => EF.Functions.LessThan(ValueTuple.Create(entry.OccurredAt, entry.Id), ValueTuple.Create(place.At, place.Id)))
            : log;

        return after
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Take(page.Limit + 1)
            .AsNoTracking();
    }
}
