using Audit.Application.AuditLogs;

namespace Audit.Application.Invitations;

/// <summary>The details of <c>tenant.owner_invited</c>: the invitation's id and the id of the invitation it replaced, never the address.</summary>
public sealed record TenantOwnerInvitedDetails(Guid InvitationId, Guid? ReplacedInvitationId) : AuditDetails;
