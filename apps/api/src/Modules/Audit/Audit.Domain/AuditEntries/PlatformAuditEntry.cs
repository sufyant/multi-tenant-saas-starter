namespace Audit.Domain.AuditEntries;

/// <summary>An entry of the platform's log: what system admins did, above tenants.</summary>
internal sealed class PlatformAuditEntry : AuditEntry
{
    public PlatformAuditEntry(Guid id, DateTimeOffset occurredAt, AuditActor actor, string operation, AuditTarget target, string? details = null)
        : base(id, occurredAt, actor, operation, target, details)
    {
    }

    private PlatformAuditEntry()
    {
    }
}
