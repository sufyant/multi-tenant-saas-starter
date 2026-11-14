using ControlPlane.Application.Users;
using ControlPlane.Contracts.Terms;
using ControlPlane.Domain.Terms;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Terms.UpdateTermsVersion;

// K6a: the version row is locked first, so an acceptance that holds it waits for the edit, or the edit for the acceptance, which it then
// counts. Every rejection comes before any change (W7). The values the version has announce nothing (I1).
public static class UpdateTermsVersionHandler
{
    public static async Task<(Result<TermsVersionView>, OutgoingMessages)> HandleAsync(
        UpdateTermsVersionCommand command,
        ITermsRepository terms,
        ITermsQueries queries,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await terms.FindVersionForUpdateAsync(command.VersionId, cancellationToken) is not { } version)
        {
            return (TermsVersion.NotFound, []);
        }

        var now = time.GetUtcNow();
        var accepted = await terms.CountAcceptancesAsync(version.Id, cancellationToken) > 0;
        var edited = version.Edit(command.EffectiveDate, command.Url, command.Summary, command.RequiresAcceptance, accepted, now);
        if (!edited.IsSuccess)
        {
            return (edited.Error, []);
        }

        var before = edited.Value;
        var after = version.Values;
        await terms.SaveChangesAsync(cancellationToken);
        var view = await queries.FindVersionAsync(version.Id, TermsVersion.DayOf(now), cancellationToken)
            ?? throw new InvalidOperationException("The version was locked in this transaction.");
        if (before == after)
        {
            return (view.Version, []);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        return (view.Version, [new TermsVersionUpdatedEvent(Guid.CreateVersion7(now), now, version.Id, actor, RecordOf(before), RecordOf(after))]);
    }

    private static TermsVersionRecord RecordOf(TermsVersionValues values) =>
        new(values.EffectiveDate, values.Url, values.Summary, values.RequiresAcceptance);
}
