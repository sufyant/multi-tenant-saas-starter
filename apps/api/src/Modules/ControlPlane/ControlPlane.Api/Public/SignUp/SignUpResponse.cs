using ControlPlane.Api.Platform.Plans;

namespace ControlPlane.Api.Public.SignUp;

/// <summary>
/// Whether the product has sign-up on, how many tenants that were not purged one person may create, and the trial a user's own new tenant
/// starts: null while billing is off (step 40d).
/// </summary>
public sealed record SignUpResponse(bool Enabled, int TenantLimit, SignUpTrialResponse? Trial);
