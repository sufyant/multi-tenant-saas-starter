using Audit.Domain;
using SharedKernel;

namespace Audit.Application.Ports;

/// <summary>
/// The audit logs: the active tenant's log and the platform's log, written in the handler's one transaction, and read a page at a time.
/// </summary>
/// <remarks>
/// Public only because Wolverine's generated code passes it to public handlers; its members speak domain types, so they
/// are internal to the module.
/// </remarks>
public interface IAuditLog
{
    /// <summary>
    /// Writes the entries of one event, unless they are stored already. An event may arrive twice; its id is each entry's id. When
    /// the event gives a tenant's entry, that entry decides: both are written in one transaction, and a transaction that declares
    /// a tenant cannot read the platform's log (R11). Should two copies race, the second fails on the entry's key, and its retry
    /// finds the entries stored.
    /// </summary>
    internal Task RecordAsync(PlatformAuditEntry? platform, TenantAuditEntry? tenant, CancellationToken cancellationToken);

    /// <summary>
    /// The entries of the active tenant's log after the cursor's place, newest first, by the moment and then the id, both descending: one
    /// more than the limit when there are, so the caller knows a next page exists. Row level security keeps the log to the tenant.
    /// </summary>
    internal Task<IReadOnlyList<AuditEntry>> ListTenantEntriesAsync(CursorRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// The entries of the platform's log, in the same order and the same way, or only those about the tenant: their target is the tenant.
    /// Only a transaction that declares neither a tenant nor a user reads them (R11).
    /// </summary>
    internal Task<IReadOnlyList<AuditEntry>> ListPlatformEntriesAsync(CursorRequest page, Guid? tenantId, CancellationToken cancellationToken);
}
