namespace ControlPlane.Application.Users;

/// <summary>A user's identity provider id, the address our record keeps (A7), and whether the user is deleted (A8).</summary>
internal sealed record UserIdentity(string ExternalId, string? Email, bool Deleted);
