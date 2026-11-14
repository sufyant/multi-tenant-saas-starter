using ControlPlane.Application.TermsVersions;
using ControlPlane.Domain.TermsVersions;
using SharedKernel;

namespace ControlPlane.Application.Users.GetUser;

// The user, with whether they accepted the terms in force and the scheduled terms that require acceptance (the maintainer's decision of
// 2026-11-14: "did this person accept?" is answered on that person's page).
public static class GetUserHandler
{
    public static async Task<Result<UserDetailView>> HandleAsync(
        GetUserQuery query,
        IUserQueries users,
        ITermsVersionQueries terms,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        await users.FindUserDetailAsync(query.UserId, cancellationToken) is { } user
            ? user with { Terms = await terms.FindUserTermsAsync(query.UserId, TermsVersion.DayOf(time.GetUtcNow()), cancellationToken) }
            : Errors.UserNotFound;
}
