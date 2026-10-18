using SharedKernel;

namespace Audit.Application.AuditLogs.ListAuditEntries;

/// <summary>
/// The active tenant's audit log, a page at a time, newest first (K5's cursor). The screen calls it "Activity". No user is
/// named yet: <see cref="GetAuditEntryNamesQuery"/> names them.
/// </summary>
public sealed record ListAuditEntriesQuery(CursorRequest Page);
