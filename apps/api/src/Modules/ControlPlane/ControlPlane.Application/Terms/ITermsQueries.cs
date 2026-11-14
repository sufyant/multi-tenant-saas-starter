using ControlPlane.Application.Terms.GetMyTerms;
using ControlPlane.Application.Terms.GetTermsInForce;
using ControlPlane.Application.Terms.ListTermsVersionPeople;
using SharedKernel;

namespace ControlPlane.Application.Terms;

/// <summary>
/// The reads of the terms (Q2). The versions, the acceptances and the users belong to no tenant (R6), so the platform counts across every
/// user, and row level security hides nothing it counts. A user who may accept is a tenant user who is active: neither suspended nor deleted,
/// and a member of at least one tenant (R13). A system admin never joins a tenant for that role, so the platform itself has no gate.
/// </summary>
/// <remarks>Public because a public handler takes it (W9); its members are internal to the module.</remarks>
public interface ITermsQueries
{
    /// <summary>Every version on this day, the newest day first, with its acceptances, and how many users may accept.</summary>
    internal Task<TermsOfUseView> GetTermsOfUseAsync(DateOnly today, CancellationToken cancellationToken);

    /// <summary>The version with this id on this day, and how many users may accept; none when no version has the id.</summary>
    internal Task<TermsVersionDetailView?> FindVersionAsync(Guid versionId, DateOnly today, CancellationToken cancellationToken);

    /// <summary>
    /// The users who may accept and accepted the version, the latest acceptance first, or those who did not, the newest user first: a page
    /// after the cursor. None when no version has the id.
    /// </summary>
    internal Task<CursorPage<TermsPersonView>?> ListPeopleAsync(
        Guid versionId,
        TermsPersonStatus status,
        CursorRequest page,
        CancellationToken cancellationToken);

    /// <summary>What the signed-in person must do about the terms on this day; nothing for a person who is not a user yet.</summary>
    internal Task<MyTermsView> GetMyTermsAsync(string externalUserId, DateOnly today, CancellationToken cancellationToken);

    /// <summary>The version in force on this day; none before the first version takes effect.</summary>
    internal Task<TermsInForceView?> FindVersionInForceAsync(DateOnly today, CancellationToken cancellationToken);
}
