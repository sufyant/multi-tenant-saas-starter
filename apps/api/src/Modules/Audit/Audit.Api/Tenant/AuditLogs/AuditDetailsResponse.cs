using System.Text.Json.Serialization;
using Audit.Application.AuditLogs;

namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// The details of an audit entry: one shape for each operation that keeps some. <c>operation</c> names the shape, so a client narrows
/// <c>details</c> by it. It is the entry's own operation.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "operation")]
[JsonDerivedType(typeof(MemberInvitedDetailsResponse), AuditDetailsTypes.MemberInvited)]
[JsonDerivedType(typeof(MemberRemovedDetailsResponse), AuditDetailsTypes.MemberRemoved)]
[JsonDerivedType(typeof(MemberRoleChangedDetailsResponse), AuditDetailsTypes.MemberRoleChanged)]
[JsonDerivedType(typeof(RoleCreatedDetailsResponse), AuditDetailsTypes.RoleCreated)]
[JsonDerivedType(typeof(RoleUpdatedDetailsResponse), AuditDetailsTypes.RoleUpdated)]
[JsonDerivedType(typeof(TenantOwnerInvitedDetailsResponse), AuditDetailsTypes.TenantOwnerInvited)]
[JsonDerivedType(typeof(TenantRenamedDetailsResponse), AuditDetailsTypes.TenantRenamed)]
[JsonDerivedType(typeof(TenantSuspendedDetailsResponse), AuditDetailsTypes.TenantSuspended)]
public abstract record AuditDetailsResponse;
