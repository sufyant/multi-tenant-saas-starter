using SharedKernel;

namespace ControlPlane.Application.TermsVersions.GetMyTerms;

/// <summary>
/// What the signed-in person must do about the terms after sign-in: nothing, accept a version, or read a notice. It runs without a
/// tenant.
/// </summary>
[MessageName("get-my-terms")]
public sealed record GetMyTermsQuery(string ActorId);
