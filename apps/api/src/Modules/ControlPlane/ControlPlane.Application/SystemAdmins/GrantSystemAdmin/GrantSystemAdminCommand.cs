using SharedKernel;

namespace ControlPlane.Application.SystemAdmins.GrantSystemAdmin;

/// <summary>A system admin makes an existing user a system admin (docs/flows/system-admins.md). It runs without a tenant.</summary>
[MessageName("grant-system-admin")]
public sealed record GrantSystemAdminCommand(string ActorId, Guid UserId);
