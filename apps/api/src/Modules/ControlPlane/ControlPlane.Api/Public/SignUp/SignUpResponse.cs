namespace ControlPlane.Api.Public.SignUp;

/// <summary>Whether the product has sign-up on, and how many tenants that were not purged one person may create.</summary>
public sealed record SignUpResponse(bool Enabled, int TenantLimit);
