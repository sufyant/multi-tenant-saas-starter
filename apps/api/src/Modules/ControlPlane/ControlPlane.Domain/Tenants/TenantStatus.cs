namespace ControlPlane.Domain.Tenants;

internal enum TenantStatus
{
    Provisioning,
    Active,
    Cancelled,

    /// <summary>A system admin suspended the tenant: its members are refused at the tenant door until it is reactivated.</summary>
    Suspended,
}
