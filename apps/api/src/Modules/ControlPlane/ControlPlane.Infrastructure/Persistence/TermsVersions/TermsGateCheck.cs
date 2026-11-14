using ControlPlane.Contracts.TermsVersions;
using ControlPlane.Domain;
using ControlPlane.Domain.TermsVersions;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence.TermsVersions;

/// <summary>
/// The terms gate on every me and tenant request: one query reads the user and, for each version, whether the user accepted it; the
/// timeline and <see cref="TermsGate"/> decide, as GetMyTerms does. Nothing is cached: a request costs that one indexed query
/// (docs/flows/terms-of-use.md, section 5). The versions and the acceptances belong to no tenant (R6).
/// </summary>
/// <remarks>Public because Wolverine's generated code may build it (W1, W9).</remarks>
public sealed class TermsGateCheck(ControlPlaneDbContext controlPlane, TimeProvider time) : ITermsGate
{
    public async Task<Result> CheckAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var user = await controlPlane.Users
            .Where(u => u.ExternalId == externalUserId)
            .Select(u => new
            {
                u.CreatedAt,
                Versions = controlPlane.TermsVersions
                    .Select(v => new GateRow(
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
        if (user is null)
        {
            return Result.Success();
        }

        var timeline = TermsTimeline.Of(user.Versions.Select(v => new TermsVersionDay(v.Id, v.EffectiveDate, v.CreatedAt, v.Cancelled)), TermsVersion.DayOf(time.GetUtcNow()));
        var accepted = user.Versions.Where(v => v.Accepted).Select(v => v.Id).ToHashSet();
        var ask = TermsGate.For(GateOf(user.Versions, timeline.CurrentId), GateOf(user.Versions, timeline.ScheduledId), accepted, new HashSet<Guid>(), user.CreatedAt);
        return ask.Action == TermsAction.Accept
            ? Error.Forbidden(ControlPlaneErrorCodes.TermsVersionAcceptanceRequired, "Accept the terms of use first.") with { TermsVersionId = ask.VersionId }
            : Result.Success();
    }

    private static TermsGateVersion? GateOf(List<GateRow> versions, Guid? id) =>
        versions.SingleOrDefault(v => v.Id == id) is { } found ? new TermsGateVersion(found.Id, found.RequiresAcceptance, found.SendsEmail, found.CreatedAt) : null;

    private sealed record GateRow(Guid Id, DateOnly EffectiveDate, DateTimeOffset CreatedAt, bool Cancelled, bool RequiresAcceptance, bool SendsEmail, bool Accepted);
}
