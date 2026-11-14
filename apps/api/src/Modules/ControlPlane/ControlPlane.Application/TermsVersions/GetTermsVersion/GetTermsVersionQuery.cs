using SharedKernel;

namespace ControlPlane.Application.TermsVersions.GetTermsVersion;

/// <summary>One terms version with its state and its acceptances, and how many users may accept.</summary>
[MessageName("get-terms-version")]
public sealed record GetTermsVersionQuery(Guid VersionId);
