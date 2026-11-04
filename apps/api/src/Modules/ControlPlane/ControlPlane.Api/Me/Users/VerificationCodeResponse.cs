namespace ControlPlane.Api.Me.Users;

/// <summary>A code went out: until when it works.</summary>
public sealed record VerificationCodeResponse(DateTimeOffset ExpiresAt);
