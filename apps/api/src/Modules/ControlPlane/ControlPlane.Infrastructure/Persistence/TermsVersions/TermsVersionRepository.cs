using ControlPlane.Application.TermsVersions;
using ControlPlane.Domain.TermsVersions;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.TermsVersions;

/// <summary>The terms versions, acceptances and dismissals for the commands. They belong to no tenant, so no row level security applies (R6).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class TermsVersionRepository(ControlPlaneDbContext controlPlane) : ITermsVersionRepository
{
    // One lock of the transaction for every creation: a second creation waits for the first to commit, then sees its version. It locks no
    // row, so an edit, a cancellation and an acceptance never wait for it.
    private const string CreationLock = "terms-version-creation";

    async Task ITermsVersionRepository.LockForCreationAsync(CancellationToken cancellationToken) =>
        await controlPlane.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({CreationLock}, 0))", cancellationToken);

    async Task<IReadOnlyList<TermsVersionDay>> ITermsVersionRepository.ListVersionDaysAsync(CancellationToken cancellationToken) =>
        await controlPlane.TermsVersions
            .Select(version => new TermsVersionDay(version.Id, version.EffectiveDate, version.CreatedAt, version.CancelledAt != null))
            .ToListAsync(cancellationToken);

    // The row is locked by its own statement, then read, so the lock never sits in a subquery that EF Core composes.
    async Task<TermsVersion?> ITermsVersionRepository.FindVersionForUpdateAsync(Guid versionId, CancellationToken cancellationToken)
    {
        await controlPlane.Database.ExecuteSqlAsync($"SELECT id FROM control_plane.terms_versions WHERE id = {versionId} FOR UPDATE", cancellationToken);
        return await controlPlane.TermsVersions.SingleOrDefaultAsync(version => version.Id == versionId, cancellationToken);
    }

    async Task<TermsVersion?> ITermsVersionRepository.FindVersionForShareAsync(Guid versionId, CancellationToken cancellationToken)
    {
        await controlPlane.Database.ExecuteSqlAsync($"SELECT id FROM control_plane.terms_versions WHERE id = {versionId} FOR SHARE", cancellationToken);
        return await controlPlane.TermsVersions.AsNoTracking().SingleOrDefaultAsync(version => version.Id == versionId, cancellationToken);
    }

    async Task<int> ITermsVersionRepository.CountAcceptancesAsync(Guid versionId, CancellationToken cancellationToken) =>
        await controlPlane.TermsAcceptances.CountAsync(acceptance => acceptance.VersionId == versionId, cancellationToken);

    // Two requests of one person may race; the key keeps one row, and the first moment stays.
    async Task ITermsVersionRepository.AcceptAsync(TermsAcceptance acceptance, CancellationToken cancellationToken) =>
        await controlPlane.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO control_plane.terms_acceptances (version_id, user_id, accepted_at)
            VALUES ({acceptance.VersionId}, {acceptance.UserId}, {acceptance.AcceptedAt})
            ON CONFLICT (version_id, user_id) DO NOTHING
            """,
            cancellationToken);

    async Task ITermsVersionRepository.DismissAsync(TermsNoticeDismissal dismissal, CancellationToken cancellationToken) =>
        await controlPlane.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO control_plane.terms_notice_dismissals (version_id, user_id, dismissed_at)
            VALUES ({dismissal.VersionId}, {dismissal.UserId}, {dismissal.DismissedAt})
            ON CONFLICT (version_id, user_id) DO NOTHING
            """,
            cancellationToken);

    void ITermsVersionRepository.Add(TermsVersion version) => controlPlane.TermsVersions.Add(version);

    Task ITermsVersionRepository.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
