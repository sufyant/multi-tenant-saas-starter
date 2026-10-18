using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>
/// The signed-in user, found by the identity provider's id. A person becomes a catalog user only through an invitation or as the first
/// system admin, so a person the catalog does not know is not found. It runs without a tenant.
/// </summary>
public sealed record GetMeQuery(string ActorId);

/// <summary>The signed-in user: the catalog's id, the copy of their profile, whether they are a system admin, and their preferences.</summary>
public sealed record MeView(Guid Id, string? Email, string? FirstName, string? LastName, bool IsSystemAdmin, Language Language, string? TimeZone);

public static class GetMeHandler
{
    public static async Task<Result<MeView>> HandleAsync(GetMeQuery query, IUserCatalog users, CancellationToken cancellationToken)
    {
        if (await users.FindUserAsync(query.ActorId, cancellationToken) is not { } user)
        {
            return Errors.UserNotFound;
        }

        return new MeView(
            user.Id, user.Email, user.FirstName, user.LastName, await users.IsSystemAdminAsync(user.Id, cancellationToken), user.Language, user.TimeZone);
    }
}
