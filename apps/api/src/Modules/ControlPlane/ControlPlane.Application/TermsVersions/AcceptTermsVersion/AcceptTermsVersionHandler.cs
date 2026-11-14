using ControlPlane.Application.TermsVersions.GetMyTerms;
using ControlPlane.Application.Users;
using ControlPlane.Domain.TermsVersions;
using SharedKernel;

namespace ControlPlane.Application.TermsVersions.AcceptTermsVersion;

// The acceptance is the record (GDPR, Article 7(1)): the user, the version and the moment, which the platform lists. A link that changed
// since the person read it, or a version that is no longer in force or scheduled, is refused, so a person never accepts a text they did not
// see. The version is locked against an edit (K6a). A second acceptance records nothing more (I1). It answers what the person must do
// next: the scheduled version may ask in turn.
public static class AcceptTermsVersionHandler
{
    public static async Task<Result<MyTermsView>> HandleAsync(
        AcceptTermsVersionCommand command,
        ITermsVersionRepository terms,
        ITermsVersionQueries queries,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserIdAsync(command.ActorId, cancellationToken) is not { } userId)
        {
            return Errors.UserNotFound;
        }

        var now = time.GetUtcNow();
        var today = TermsVersion.DayOf(now);
        var version = await TermsAcceptances.FindAcceptableAsync(terms, command.VersionId, command.Url, inForceOnly: false, today, cancellationToken);
        if (!version.IsSuccess)
        {
            return version.Error;
        }

        await terms.AcceptAsync(new TermsAcceptance(version.Value.Id, userId, now), cancellationToken);
        return await queries.GetMyTermsAsync(command.ActorId, today, cancellationToken);
    }
}
