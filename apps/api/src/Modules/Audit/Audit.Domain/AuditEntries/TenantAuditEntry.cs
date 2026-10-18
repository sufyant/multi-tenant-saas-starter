using SharedKernel;

namespace Audit.Domain.AuditEntries;

/// <summary>An entry of a tenant's own log, under that tenant (row level security).</summary>
internal sealed class TenantAuditEntry : AuditEntry, ITenantEntity
{
    private TenantAuditEntry()
    {
    }

    public TenantAuditEntry(Guid id, DateTimeOffset occurredAt, AuditActor actor, string operation, AuditTarget target, string? details = null)
        : base(id, occurredAt, actor, operation, target, details)
    {
    }
}
