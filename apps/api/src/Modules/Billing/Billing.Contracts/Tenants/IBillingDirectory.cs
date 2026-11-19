namespace Billing.Contracts.Tenants;

/// <summary>
/// What billing knows of the tenants, for ControlPlane's platform screens: the billing filter of the Organisations list (4.38) and the
/// platform's plan change (4.27). It reads billing's tables that belong to no tenant (R6), so it reads across tenants.
/// </summary>
public interface IBillingDirectory
{
    /// <summary>Whether the deployment's billing is on (B1).</summary>
    bool Enabled { get; }

    /// <summary>The tenants in this billing situation now; none while billing is off.</summary>
    Task<IReadOnlyList<Guid>> ListTenantIdsAsync(BillingSituation situation, CancellationToken cancellationToken);

    /// <summary>How many tenants are in each billing situation now; none while billing is off.</summary>
    Task<BillingSituationCounts?> CountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether the tenant pays its plan by card in the app now: its subscription has not ended and it is not Contract (B6, B12). Its plan
    /// then follows its subscription, so the platform does not set it. False while billing is off.
    /// </summary>
    Task<bool> PaysByCardAsync(Guid tenantId, CancellationToken cancellationToken);
}
