using SharedKernel;

namespace ControlPlane.Application.Terms.GetTermsInForce;

/// <summary>The terms version in force, for anyone, signed in or not; none before the first version takes effect.</summary>
[MessageName("get-terms-in-force")]
public sealed record GetTermsInForceQuery;
