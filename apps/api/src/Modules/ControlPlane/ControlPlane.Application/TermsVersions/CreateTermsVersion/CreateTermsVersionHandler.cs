using ControlPlane.Application.Users;
using ControlPlane.Contracts.TermsVersions;
using ControlPlane.Domain.TermsVersions;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.TermsVersions.CreateTermsVersion;

// A version belongs to no tenant. The versions are locked against another creation first, so the check that none is scheduled holds until
// the commit (K6). Every rejection comes before any change (W7).
public static class CreateTermsVersionHandler
{
    public static async Task<(Result<TermsVersionView>, OutgoingMessages)> HandleAsync(
        CreateTermsVersionCommand command,
        ITermsVersionRepository terms,
        ITermsVersionQueries queries,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var today = TermsVersion.DayOf(now);
        await terms.LockForCreationAsync(cancellationToken);
        var timeline = TermsTimeline.Of(await terms.ListVersionDaysAsync(cancellationToken), today);
        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        var created = TermsVersion.Create(
            Guid.CreateVersion7(now), command.EffectiveDate, command.Url, command.Summary, command.RequiresAcceptance, actor, timeline.ScheduledId is not null, now);
        if (!created.IsSuccess)
        {
            return (created.Error, []);
        }

        var version = created.Value;
        terms.Add(version);
        await terms.SaveChangesAsync(cancellationToken);
        var view = await queries.FindVersionAsync(version.Id, today, cancellationToken)
            ?? throw new InvalidOperationException("The version was added in this transaction.");
        return (view.Version, [new TermsVersionCreatedEvent(
            Guid.CreateVersion7(now), now, version.Id, actor, version.EffectiveDate, version.Url, version.Summary, version.RequiresAcceptance)]);
    }
}
