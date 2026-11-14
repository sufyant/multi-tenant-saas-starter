using SharedKernel;

namespace ControlPlane.Application.TermsVersions.UpdateTermsVersion;

/// <summary>
/// A system admin gives the scheduled terms version these values. Once someone accepted it, its link and whether it requires acceptance
/// stay; its day and summary still change.
/// </summary>
[MessageName("update-terms-version")]
public sealed record UpdateTermsVersionCommand(
    string ActorId,
    Guid VersionId,
    DateOnly EffectiveDate,
    string Url,
    string? Summary,
    bool RequiresAcceptance);
