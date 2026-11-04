namespace ControlPlane.Api.Me.Users;

/// <summary>The new address, to which the API sends a code.</summary>
public sealed record RequestEmailChangeRequest(string Email);
