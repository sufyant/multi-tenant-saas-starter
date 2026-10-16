namespace ControlPlane.Domain.Tenants;

internal enum TenantStatus
{
    Provisioning,
    Active,
    Cancelled,

    /// <summary>A system admin suspended the tenant: its members are refused at the tenant door until it is reactivated.</summary>
    Suspended,

    /// <summary>
    /// A system admin closed the tenant: its members are refused at the tenant door, its data stays, and it can be reopened within the
    /// reopen period (K4).
    /// </summary>
    Closed,
}
