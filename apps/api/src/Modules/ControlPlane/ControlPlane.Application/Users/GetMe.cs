using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>
/// The signed-in user, found by the identity provider's id. A person becomes a catalog user only through an invitation or as the first
/// system admin, so a person the catalog does not know is not found. It runs without a tenant.
/// </summary>
public sealed record GetMe(string UserId);

/// <summary>The signed-in user: the catalog's id, the copy of their profile, and whether they are a system admin.</summary>
public sealed record MeDetails(Guid Id, string? Email, string? FirstName, string? LastName, bool IsSystemAdmin);

public static class GetMeHandler
{
    public static async Task<Result<MeDetails>> HandleAsync(GetMe query, IUserCatalog users, CancellationToken cancellationToken)
    {
        if (await users.FindUserAsync(query.UserId, cancellationToken) is not { } user)
        {
            return Errors.UserNotFound;
        }

        return new MeDetails(user.Id, user.Email, user.FirstName, user.LastName, await users.IsSystemAdminAsync(user.Id, cancellationToken));
    }
}
