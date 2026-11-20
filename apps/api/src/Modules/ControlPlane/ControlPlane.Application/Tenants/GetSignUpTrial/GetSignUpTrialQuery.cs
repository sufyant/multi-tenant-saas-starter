using SharedKernel;

namespace ControlPlane.Application.Tenants.GetSignUpTrial;

/// <summary>The trial that a user's own new tenant starts, for the sign-up's pages (1.28) and the platform's Give trial (4.29).</summary>
[MessageName("get-sign-up-trial")]
public sealed record GetSignUpTrialQuery;
