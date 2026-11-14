using SharedKernel;

namespace ControlPlane.Contracts.TermsVersions;

/// <summary>
/// The terms gate as the host asks it on every me and tenant request (docs/flows/terms-of-use.md, section 5): whether the signed-in person
/// may go on, by the rule of GetMyTerms, so the API and the screens never disagree.
/// </summary>
public interface ITermsGate
{
    /// <summary>
    /// Refuses a user whom GetMyTerms would ask to accept a version, with 403 <c>terms_version.acceptance_required</c> and that version.
    /// A notice refuses nothing, and neither does a person who is not a user yet.
    /// </summary>
    Task<Result> CheckAsync(string externalUserId, CancellationToken cancellationToken);
}
