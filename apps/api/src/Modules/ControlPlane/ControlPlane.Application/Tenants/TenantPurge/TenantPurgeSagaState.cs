namespace ControlPlane.Application.Tenants.TenantPurge;

/// <summary>Where the tenant purge stands. It is the saga's own state (S3): the tenant's status says only that the purge runs (S11).</summary>
public enum TenantPurgeSagaState
{
    /// <summary>The modules delete their data of the tenant; the purge waits for each one's reply.</summary>
    PurgingModules,

    /// <summary>Every module replied; ControlPlane deletes its own data and leaves the tenant's tombstone.</summary>
    Finishing,

    /// <summary>The tenant is purged.</summary>
    Completed,
}
