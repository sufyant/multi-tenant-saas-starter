using SharedKernel;

namespace ControlPlane.Application.TermsVersions.CancelTermsVersion;

/// <summary>A system admin cancels the scheduled terms version before it takes effect. Its acceptances stay with it.</summary>
[MessageName("cancel-terms-version")]
public sealed record CancelTermsVersionCommand(string ActorId, Guid VersionId);
