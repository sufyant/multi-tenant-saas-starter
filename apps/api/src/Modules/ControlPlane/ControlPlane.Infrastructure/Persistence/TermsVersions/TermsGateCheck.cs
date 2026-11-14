using ControlPlane.Contracts.TermsVersions;
using ControlPlane.Domain;
using ControlPlane.Domain.TermsVersions;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Tenancy.Caching;

namespace ControlPlane.Infrastructure.Persistence.TermsVersions;

/// <summary>
/// The terms gate on every me and tenant request (A11). One query reads the user and, for each version, whether the user accepted it,
/// through the cache (<see cref="CachedReads.UserTerms"/>). The timeline and <see cref="TermsGate"/> then decide on the day of the request, as
/// GetMyTerms does, so a scheduled version takes effect at its day with no write. The versions and the acceptances belong to no tenant (R6).
/// </summary>
internal sealed class TermsGateCheck(ControlPlaneDbContext controlPlane, ReadCache cache, TimeProvider time) : ITermsGate
{
    public async Task<Result> CheckAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var rows = await cache.ReadAsync(CachedReads.UserTerms, externalUserId, token => ReadAsync(externalUserId, token), cancellationToken);
        if (!rows.IsUser)
        {
            return Result.Success();
        }

        var timeline = TermsTimeline.Of(rows.Versions.Select(v => new TermsVersionDay(v.Id, v.EffectiveDate, v.CreatedAt, v.Cancelled)), TermsVersion.DayOf(time.GetUtcNow()));
        var accepted = rows.Versions.Where(v => v.Accepted).Select(v => v.Id).ToHashSet();

        // Only the request to accept refuses; a notice never does, so the gate's notice rule needs neither the dismissals nor the user's age.
        var ask = TermsGate.For(GateOf(rows, timeline.CurrentId), GateOf(rows, timeline.ScheduledId), accepted, new HashSet<Guid>(), DateTimeOffset.MaxValue);
        return ask.Action == TermsAction.Accept
            ? Error.Forbidden(ControlPlaneErrorCodes.TermsVersionAcceptanceRequired, "Accept the terms of use first.") with { TermsVersionId = ask.VersionId }
            : Result.Success();
    }

    private static TermsGateVersion? GateOf(TermsGateRows rows, Guid? id) =>
        rows.Versions.SingleOrDefault(v => v.Id == id) is { } found ? new TermsGateVersion(found.Id, found.RequiresAcceptance, found.SendsEmail, found.CreatedAt) : null;

    private async Task<TermsGateRows> ReadAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var user = await controlPlane.Users
            .Where(u => u.ExternalId == externalUserId)
            .Select(u => new
            {
                Versions = controlPlane.TermsVersions
                    .Select(v => new TermsGateVersionRow(
                        v.Id,
                        v.EffectiveDate,
                        v.CreatedAt,
                        v.CancelledAt != null,
                        v.RequiresAcceptance,
                        v.SendsEmail,
                        controlPlane.TermsAcceptances.Any(a => a.VersionId == v.Id && a.UserId == u.Id)))
                    .ToList(),
            })
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return user is null ? new TermsGateRows(false, []) : new TermsGateRows(true, [.. user.Versions]);
    }
}
