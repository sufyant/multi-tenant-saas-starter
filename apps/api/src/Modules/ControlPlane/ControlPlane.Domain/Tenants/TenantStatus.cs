namespace ControlPlane.Domain.Tenants;

internal enum TenantStatus
{
    /// <summary>A system admin created the tenant, or gave it back to its members: they act in it.</summary>
    Active,

    /// <summary>A system admin suspended the tenant: its members are refused at the tenant door until it is reactivated.</summary>
    Suspended,

    /// <summary>
    /// A system admin closed the tenant: its members are refused at the tenant door, its data stays, and it can be reopened within the
    /// reopen period (K4).
    /// </summary>
    Closed,

    /// <summary>
    /// The closed tenant's purge has started: the pivot of the purge (S5). It cannot be reopened, and its members are refused at the
    /// tenant door as a closed tenant's are, until the purge's last step takes their memberships away (K4).
    /// </summary>
    Purging,

    /// <summary>
    /// The purge has ended: the tenant row is a tombstone that keeps the id, the name, the slug, the status, the moment of its creation
    /// and the moment of the purge, and no member, role, invitation or other data (K4). The slug is never given again.
    /// </summary>
    Purged,
}
