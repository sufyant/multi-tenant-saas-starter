using System.Linq.Expressions;
using ControlPlane.Application.TermsVersions;
using ControlPlane.Application.TermsVersions.GetMyTerms;
using ControlPlane.Application.TermsVersions.GetTermsInForce;
using ControlPlane.Application.TermsVersions.ListTermsVersionPeople;
using ControlPlane.Domain.TermsVersions;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence.TermsVersions;

/// <summary>
/// The terms for the queries, read without tracking (Q2). The versions are few, one for each change of the terms, so a read takes them all
/// and the timeline gives each its state; the counts are one query each. The versions, the acceptances and the users belong to no tenant
/// (R6), so nothing is hidden from a count.
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class TermsVersionQueries(ControlPlaneDbContext controlPlane) : ITermsVersionQueries
{
    // A user who may accept: a tenant user who may still act (A9), and a member of at least one tenant (R13). A system admin who joined no
    // tenant is none.
    internal static readonly Expression<Func<User, bool>> MayAccept = user => user.DeletedAt == null && user.SuspendedAt == null && user.MembershipCount > 0;

    async Task<TermsOfUseView> ITermsVersionQueries.GetTermsOfUseAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var versions = await controlPlane.TermsVersions.AsNoTracking().ToListAsync(cancellationToken);
        var accepted = await controlPlane.TermsAcceptances
            .Join(controlPlane.Users.Where(MayAccept), acceptance => acceptance.UserId, user => user.Id, (acceptance, _) => acceptance.VersionId)
            .GroupBy(versionId => versionId)
            .Select(group => new { VersionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.VersionId, group => group.Count, cancellationToken);
        var locked = (await controlPlane.TermsAcceptances.Select(acceptance => acceptance.VersionId).Distinct().ToListAsync(cancellationToken)).ToHashSet();
        var activeUsers = await controlPlane.Users.CountAsync(MayAccept, cancellationToken);
        var timeline = TermsTimeline.Of(versions.Select(DayOf), today);

        return new TermsOfUseView(
            [.. versions
                .OrderByDescending(version => version.EffectiveDate)
                .ThenByDescending(version => version.CreatedAt)
                .ThenByDescending(version => version.Id)
                .Select(version => new TermsVersionView(
                    version.Id,
                    version.EffectiveDate,
                    version.Url,
                    version.Summary,
                    version.RequiresAcceptance,
                    timeline.StateOf(version.Id).ToString(),
                    accepted.GetValueOrDefault(version.Id),
                    locked.Contains(version.Id),
                    version.CreatedAt,
                    version.CancelledAt))],
            activeUsers);
    }

    async Task<TermsVersionDetailView?> ITermsVersionQueries.FindVersionAsync(Guid versionId, DateOnly today, CancellationToken cancellationToken)
    {
        var terms = await ((ITermsVersionQueries)this).GetTermsOfUseAsync(today, cancellationToken);
        return terms.Versions.SingleOrDefault(version => version.Id == versionId) is { } found ? new TermsVersionDetailView(found, terms.ActiveUserCount) : null;
    }

    // Accepted: the latest acceptance first, by the moment and then the user. Pending: the newest user first, by the moment they became a
    // user and then the user. The cursor's place is compared as one row value (K5).
    async Task<CursorPage<TermsPersonView>?> ITermsVersionQueries.ListPeopleAsync(
        Guid versionId,
        TermsPersonStatus status,
        CursorRequest page,
        CancellationToken cancellationToken)
    {
        if (!await controlPlane.TermsVersions.AnyAsync(version => version.Id == versionId, cancellationToken))
        {
            return null;
        }

        var people = controlPlane.Users.Where(MayAccept);
        var acceptances = controlPlane.TermsAcceptances.Where(acceptance => acceptance.VersionId == versionId);
        List<TermsPersonView> found;
        if (status == TermsPersonStatus.Accepted)
        {
            var rows = acceptances.Join(people, acceptance => acceptance.UserId, user => user.Id, (acceptance, user) => new { acceptance.AcceptedAt, User = user });
            var after = page.After is { } place
                ? rows.Where(row => EF.Functions.LessThan(ValueTuple.Create(row.AcceptedAt, row.User.Id), ValueTuple.Create(place.At, place.Id)))
                : rows;
            found = await after
                .OrderByDescending(row => row.AcceptedAt)
                .ThenByDescending(row => row.User.Id)
                .Take(page.Limit + 1)
                .Select(row => new TermsPersonView(row.User.Id, row.User.Name, row.User.Email, row.AcceptedAt, row.User.CreatedAt))
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            return CursorPage.Of(found, page, person => new CursorPosition(person.AcceptedAt!.Value, person.UserId));
        }

        var pending = people.Where(user => !acceptances.Any(acceptance => acceptance.UserId == user.Id));
        var next = page.After is { } at
            ? pending.Where(user => EF.Functions.LessThan(ValueTuple.Create(user.CreatedAt, user.Id), ValueTuple.Create(at.At, at.Id)))
            : pending;
        found = await next
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .Take(page.Limit + 1)
            .Select(user => new TermsPersonView(user.Id, user.Name, user.Email, null, user.CreatedAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return CursorPage.Of(found, page, person => new CursorPosition(person.CreatedAt, person.UserId));
    }

    async Task<MyTermsView> ITermsVersionQueries.GetMyTermsAsync(string externalUserId, DateOnly today, CancellationToken cancellationToken)
    {
        var user = await controlPlane.Users
            .Where(u => u.ExternalId == externalUserId)
            .Select(u => new { u.Id, u.CreatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new MyTermsView(TermsAction.None.ToString(), null);
        }

        var versions = await controlPlane.TermsVersions.AsNoTracking().ToListAsync(cancellationToken);
        var timeline = TermsTimeline.Of(versions.Select(DayOf), today);
        var accepted = (await controlPlane.TermsAcceptances.Where(a => a.UserId == user.Id).Select(a => a.VersionId).ToListAsync(cancellationToken)).ToHashSet();
        var dismissed = (await controlPlane.TermsNoticeDismissals.Where(d => d.UserId == user.Id).Select(d => d.VersionId).ToListAsync(cancellationToken)).ToHashSet();
        var ask = TermsGate.For(GateOf(versions, timeline.CurrentId), GateOf(versions, timeline.ScheduledId), accepted, dismissed, user.CreatedAt);
        var version = versions.SingleOrDefault(v => v.Id == ask.VersionId);
        return new MyTermsView(
            ask.Action.ToString(),
            version is null ? null : new MyTermsVersionView(version.Id, version.EffectiveDate, version.Url, version.Summary, version.RequiresAcceptance));
    }

    async Task<TermsInForceView?> ITermsVersionQueries.FindVersionInForceAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var versions = await controlPlane.TermsVersions.AsNoTracking().ToListAsync(cancellationToken);
        var current = TermsTimeline.Of(versions.Select(DayOf), today).CurrentId;
        return versions.SingleOrDefault(version => version.Id == current) is { } found ? new TermsInForceView(found.Id, found.EffectiveDate, found.Url) : null;
    }

    private static TermsVersionDay DayOf(TermsVersion version) => new(version.Id, version.EffectiveDate, version.CreatedAt, version.CancelledAt is not null);

    private static TermsGateVersion? GateOf(IReadOnlyList<TermsVersion> versions, Guid? id) =>
        versions.SingleOrDefault(version => version.Id == id) is { } found ? new TermsGateVersion(found.Id, found.RequiresAcceptance, found.CreatedAt) : null;
}
