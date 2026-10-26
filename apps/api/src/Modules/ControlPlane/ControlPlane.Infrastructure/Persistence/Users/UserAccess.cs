namespace ControlPlane.Infrastructure.Persistence.Users;

/// <summary>What the cache keeps of a user for the check on every me and tenant route (A9, <see cref="CachedReads.UserAccess"/>).</summary>
internal sealed record UserAccess(bool Deleted, bool Suspended);
