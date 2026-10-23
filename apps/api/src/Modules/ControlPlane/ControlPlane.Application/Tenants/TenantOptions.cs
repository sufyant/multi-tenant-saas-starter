namespace ControlPlane.Application.Tenants;

/// <summary>
/// Configuration of the tenant lifecycle (section <c>ControlPlane:Tenants</c>), checked on start; public because a handler takes it (W9).
/// </summary>
public sealed class TenantOptions
{
    public const string Section = "ControlPlane:Tenants";

    /// <summary>How long after its closure a system admin can reopen a tenant (K4). When it ends, the tenant's purge starts.</summary>
    public TimeSpan ReopenPeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// How long the tenant purge waits for its steps before it raises its alarm (S10). A healthy purge ends in seconds: each step is one
    /// transaction, which a failure tries three times within seconds before the dead letter queue (W11). An hour also covers a worker that
    /// restarts or a deploy, so an alarm means a step that failed for good or a worker that does not run, and a person still sees it the
    /// same working day.
    /// </summary>
    public TimeSpan PurgeTimeout { get; set; } = TimeSpan.FromHours(1);
}
