using SharedKernel;

namespace ControlPlane.Application.TermsVersions.UpdateTermsVersion;

/// <summary>
/// A system admin gives the scheduled terms version these values. Once someone accepted it, its link and whether it requires acceptance
/// stay; its day and summary still change. Its email may turn on, and then goes; once it went, it stays on.
/// </summary>
[MessageName("update-terms-version")]
public sealed record UpdateTermsVersionCommand(
    string ActorId,
    Guid VersionId,
    DateOnly EffectiveDate,
    string Url,
    string? Summary,
    bool RequiresAcceptance,
    bool SendsEmail);
