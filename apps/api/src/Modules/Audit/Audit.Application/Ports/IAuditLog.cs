using Audit.Domain;

namespace Audit.Application.Ports;

/// <summary>The audit logs: the active tenant's log and the platform's log, written in the handler's one transaction.</summary>
/// <remarks>
/// Public only because Wolverine's generated code passes it to public handlers; its members speak domain types, so they
/// are internal to the module.
/// </remarks>
public interface IAuditLog
{
    /// <summary>
    /// Writes the records of one event, unless they are stored already. An event may arrive twice; its id is each record's id. When
    /// the event gives a tenant's record, that record decides: both are written in one transaction, and a transaction that declares
    /// a tenant cannot read the platform's log (R11). Should two copies race, the second fails on the record's key, and its retry
    /// finds the records stored.
    /// </summary>
    internal Task RecordAsync(PlatformAuditEntry? platform, AuditEntry? tenant, CancellationToken cancellationToken);
}
