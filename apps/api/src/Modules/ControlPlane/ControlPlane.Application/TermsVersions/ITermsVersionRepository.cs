using ControlPlane.Domain.TermsVersions;

namespace ControlPlane.Application.TermsVersions;

/// <summary>
/// The terms versions and the users' acceptances and dismissals, for the commands. They belong to no tenant (R6), so a command reaches them
/// in any transaction, a tenant's included, as accepting an invitation does.
/// </summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface ITermsVersionRepository
{
    /// <summary>
    /// Locks the versions against another creation for the rest of the transaction, so two system admins never both create a scheduled
    /// version (K6). A read of the versions goes on; an acceptance waits for nothing.
    /// </summary>
    internal Task LockForCreationAsync(CancellationToken cancellationToken);

    /// <summary>Every version as its state reads it (<see cref="TermsTimeline"/>).</summary>
    internal Task<IReadOnlyList<TermsVersionDay>> ListVersionDaysAsync(CancellationToken cancellationToken);

    /// <summary>The version with this id, locked for the rest of the transaction, so its edits and its acceptances run one after the other (K6a).</summary>
    internal Task<TermsVersion?> FindVersionForUpdateAsync(Guid versionId, CancellationToken cancellationToken);

    /// <summary>
    /// The version with this id, locked against an edit for the rest of the transaction but not against another acceptance, so a person
    /// accepts the link they read: an edit that changes it waits, and then finds the acceptance (K6a).
    /// </summary>
    internal Task<TermsVersion?> FindVersionForShareAsync(Guid versionId, CancellationToken cancellationToken);

    /// <summary>How many users accepted the version, whatever their state.</summary>
    internal Task<int> CountAcceptancesAsync(Guid versionId, CancellationToken cancellationToken);

    /// <summary>Records the acceptance; a second acceptance of the same version by the same user records nothing more (O4).</summary>
    internal Task AcceptAsync(TermsAcceptance acceptance, CancellationToken cancellationToken);

    /// <summary>Records the dismissal; a second one of the same notice by the same user records nothing more.</summary>
    internal Task DismissAsync(TermsNoticeDismissal dismissal, CancellationToken cancellationToken);

    /// <summary>Adds the new version; the caller saves.</summary>
    internal void Add(TermsVersion version);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
