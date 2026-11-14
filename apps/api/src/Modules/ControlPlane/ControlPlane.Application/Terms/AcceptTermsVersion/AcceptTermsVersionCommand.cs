using SharedKernel;

namespace ControlPlane.Application.Terms.AcceptTermsVersion;

/// <summary>
/// The signed-in person accepts a terms version, with the link they read: the version in force or the scheduled one. It runs without a
/// tenant.
/// </summary>
[MessageName("accept-terms-version")]
public sealed record AcceptTermsVersionCommand(string ActorId, Guid VersionId, string Url);
