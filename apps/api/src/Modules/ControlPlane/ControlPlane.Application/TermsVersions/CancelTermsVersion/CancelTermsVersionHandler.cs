using ControlPlane.Application.Users;
using ControlPlane.Contracts.TermsVersions;
using ControlPlane.Domain.TermsVersions;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.TermsVersions.CancelTermsVersion;

// K6a: the version row is locked first. A cancelled version answers as it is and announces nothing (I1).
public static class CancelTermsVersionHandler
{
    public static async Task<(Result<TermsVersionView>, OutgoingMessages)> HandleAsync(
        CancelTermsVersionCommand command,
        ITermsVersionRepository terms,
        ITermsVersionQueries queries,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await terms.FindVersionForUpdateAsync(command.VersionId, cancellationToken) is not { } version)
        {
            return (TermsVersion.NotFound, []);
        }

        var now = time.GetUtcNow();
        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        var cancelled = version.Cancel(actor, now);
        if (!cancelled.IsSuccess)
        {
            return (cancelled.Error, []);
        }

        await terms.SaveChangesAsync(cancellationToken);
        var view = await queries.FindVersionAsync(version.Id, TermsVersion.DayOf(now), cancellationToken)
            ?? throw new InvalidOperationException("The version was locked in this transaction.");
        if (cancelled.Value == StateChange.Unchanged)
        {
            return (view.Version, []);
        }

        return (view.Version, [new TermsVersionCancelledEvent(
            Guid.CreateVersion7(now),
            now,
            version.Id,
            actor,
            version.EffectiveDate,
            version.Url,
            version.RequiresAcceptance,
            await terms.CountAcceptancesAsync(version.Id, cancellationToken))]);
    }
}
