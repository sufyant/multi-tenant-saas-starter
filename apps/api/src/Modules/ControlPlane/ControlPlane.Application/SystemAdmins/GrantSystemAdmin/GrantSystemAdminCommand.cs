using SharedKernel;

namespace ControlPlane.Application.SystemAdmins.GrantSystemAdmin;

/// <summary>A system admin makes an existing user a system admin (section 6, "Root system admins"). It runs without a tenant.</summary>
[MessageName("grant-system-admin")]
public sealed record GrantSystemAdminCommand(string ActorId, Guid UserId);
