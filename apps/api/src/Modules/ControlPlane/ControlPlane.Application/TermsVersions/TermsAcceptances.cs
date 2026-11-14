using ControlPlane.Domain;
using ControlPlane.Domain.TermsVersions;
using SharedKernel;

namespace ControlPlane.Application.TermsVersions;

/// <summary>
/// Which version a person may accept. The gate accepts the version in force or the scheduled one, with the link the person read; accepting
/// an invitation accepts the version in force, which never changes.
/// </summary>
internal static class TermsAcceptances
{
    public static readonly Error Changed = Error.Conflict(
        ControlPlaneErrorCodes.TermsVersionChanged, "The terms changed since you read them. Read them again before you accept.");

    /// <summary>
    /// The version, locked against an edit, when the person may accept it now: in force or scheduled, and, when a link is given, with that
    /// link. None and a refusal otherwise.
    /// </summary>
    public static async Task<Result<TermsVersion>> FindAcceptableAsync(
        ITermsVersionRepository terms,
        Guid versionId,
        string? url,
        bool inForceOnly,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (await terms.FindVersionForShareAsync(versionId, cancellationToken) is not { } version)
        {
            return TermsVersion.NotFound;
        }

        var state = TermsTimeline.Of(await terms.ListVersionDaysAsync(cancellationToken), today).StateOf(version.Id);
        var acceptable = state == TermsVersionState.Current || (!inForceOnly && state == TermsVersionState.Scheduled);
        return acceptable && (url is null || url.Trim() == version.Url) ? version : Changed;
    }
}
