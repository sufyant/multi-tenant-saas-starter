namespace ControlPlane.Contracts.Members;

/// <summary>
/// The members of the tenant that the caller's transaction declares, as another module reads them: a handler of an event of that tenant,
/// whose envelope carries it (W3). Row level security keeps the read to that tenant (R1).
/// </summary>
public interface IMemberDirectory
{
    /// <summary>The user ids of the tenant's Owners at this moment: the members whose role holds <c>tenant.owners.manage</c> (A1).</summary>
    Task<IReadOnlyList<Guid>> ListOwnerIdsAsync(CancellationToken cancellationToken);

    /// <summary>The permissions the user's role holds in the tenant, or none when the user is no member.</summary>
    Task<IReadOnlySet<string>?> FindPermissionsAsync(Guid userId, CancellationToken cancellationToken);
}
