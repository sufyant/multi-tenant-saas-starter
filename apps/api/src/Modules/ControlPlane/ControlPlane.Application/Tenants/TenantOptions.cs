namespace ControlPlane.Application.Tenants;

/// <summary>
/// Configuration of the tenant lifecycle (section <c>ControlPlane:Tenants</c>), checked on start; public because a handler takes it (W9).
/// </summary>
public sealed class TenantOptions
{
    public const string Section = "ControlPlane:Tenants";

    /// <summary>How long after its closure a system admin can reopen a tenant (K4).</summary>
    public TimeSpan ReopenPeriod { get; set; } = TimeSpan.FromDays(30);
}
