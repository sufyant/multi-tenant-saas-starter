using Audit.Application.AuditLogs;

namespace Audit.Application.Members;

/// <summary>
/// The details of <c>tenant.owner_made</c>: the member who became an Owner, by id, and the role they had, by its name at that moment,
/// its id and whether it is built in. The entry's target is the tenant, so the member is here.
/// </summary>
public sealed record TenantOwnerMadeDetails(Guid UserId, string OldRole, Guid OldRoleId, bool OldRoleBuiltIn) : AuditDetails;
