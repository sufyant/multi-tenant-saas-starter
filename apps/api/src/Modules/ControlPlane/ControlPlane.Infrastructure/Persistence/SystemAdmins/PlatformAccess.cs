namespace ControlPlane.Infrastructure.Persistence.SystemAdmins;

/// <summary>
/// What the cache keeps of the system door (A4, <see cref="CachedReads.PlatformAccess"/>): whether the person is a user, and a system admin.
/// The second factor belongs to the session, so it stays out (A6).
/// </summary>
internal sealed record PlatformAccess(bool Known, bool IsSystemAdmin);
