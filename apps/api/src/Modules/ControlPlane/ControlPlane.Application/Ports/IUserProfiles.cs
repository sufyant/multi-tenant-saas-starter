using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Ports;

/// <summary>
/// The catalog users, outside any tenant, for the copy of their profile. Users belong to no tenant (R6), so the transaction declares
/// neither a tenant nor a user.
/// </summary>
/// <remarks>Public only because Wolverine's generated code passes it to public handlers.</remarks>
public interface IUserProfiles
{
    /// <summary>The user with this identity provider id, locked for the rest of the transaction, so two events change it one at a time.</summary>
    internal Task<User?> FindUserForUpdateAsync(string externalUserId, CancellationToken cancellationToken);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
