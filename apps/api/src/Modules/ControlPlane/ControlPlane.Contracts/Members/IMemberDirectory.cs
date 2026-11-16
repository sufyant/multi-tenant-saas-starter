namespace ControlPlane.Contracts.Members;

/// <summary>
/// The members of the tenant that the caller's transaction declares, as another module reads them: a handler of an event of that tenant,
/// whose envelope carries it (W3). Row level security keeps the read to that tenant (R1).
/// </summary>
public interface IMemberDirectory
{
    /// <summary>The user ids of the tenant's Owners at this moment: the members whose role holds <c>tenant.owners.manage</c> (A1).</summary>
    Task<IReadOnlyList<Guid>> ListOwnerIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The user ids of the members who manage the tenant's billing at this moment: the members whose role holds <c>tenant.billing.manage</c>,
    /// the Owners, the Billing managers and any custom role with it (A1, step 40).
    /// </summary>
    Task<IReadOnlyList<Guid>> ListBillingManagerIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// How many of the tenant's members take a seat: every member whose role is not the Billing manager (A2). Billing charges the fee per
    /// person for each of them; a pending invitation is not charged until it is accepted (step 40).
    /// </summary>
    Task<int> CountSeatedMembersAsync(CancellationToken cancellationToken);
}
