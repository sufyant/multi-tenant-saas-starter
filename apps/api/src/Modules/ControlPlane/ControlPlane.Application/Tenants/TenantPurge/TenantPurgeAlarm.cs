using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace ControlPlane.Application.Tenants.TenantPurge;

/// <summary>
/// Tells a person that a tenant's purge waits too long (S10): an error log entry with a fixed event name, the tenant's id and the steps it
/// still waits for, and one counter. It names no exception: a step that failed for good is in the dead letter queue.
/// </summary>
/// <remarks>Public because a public handler takes it (W9).</remarks>
public sealed partial class TenantPurgeAlarm(ILogger<TenantPurgeAlarm> logger, IMeterFactory meters)
{
    /// <summary>The module's meter; the host exports every meter named <c>Modules.*</c>.</summary>
    public const string MeterName = "Modules.ControlPlane";

    private readonly Counter<long> _timedOut = meters.Create(MeterName).CreateCounter<long>(
        "tenant_purge.timed_out",
        description: "Tenant purges that waited past their timeout for a step.");

    public void Raise(Guid tenantId, IReadOnlyList<string> waiting)
    {
        LogTimedOut(logger, tenantId, string.Join(", ", waiting));
        _timedOut.Add(1);
    }

    [LoggerMessage(EventId = 2, EventName = "TenantPurgeTimedOut", Level = LogLevel.Error, Message = "The purge of tenant {TenantId} still waits for {Waiting}")]
    private static partial void LogTimedOut(ILogger logger, Guid tenantId, string waiting);
}
