using SharedKernel;

namespace Audit.Application.AuditLogs.DeleteExpiredEntries;

/// <summary>
/// The second step of one tenant's retention, in that tenant's transaction (K14): the tenant's period at the first step's moment, in days,
/// none for no limit, and that moment.
/// </summary>
/// <param name="Readings">How many times this run read the period again for an old message, at most <c>DeleteExpiredEntriesHandler.MaxReadings</c>.</param>
[MessageName("delete-expired-entries")]
public sealed record DeleteExpiredEntriesCommand(Guid TenantId, int? Days, DateTimeOffset ReadAt, int Readings);
