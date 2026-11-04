namespace ControlPlane.Application.Users;

/// <summary>A code that went out, as the screen says it: until when it works.</summary>
public sealed record VerificationCodeView(DateTimeOffset ExpiresAt);
