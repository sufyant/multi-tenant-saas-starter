namespace ControlPlane.Api.Me.Users;

/// <summary>The six digits that went to the new address.</summary>
public sealed record ChangeEmailRequest(string Code);
