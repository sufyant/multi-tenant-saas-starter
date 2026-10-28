using Audit.Application.AuditLogs;
using Audit.Application.Plans;

namespace Audit.Application.Tenants;

/// <summary>
/// The details of <c>tenant.created</c>: the plan the tenant got, the default plan when the request named none, in both logs. An entry of
/// the version before step 32 has no details.
/// </summary>
public sealed record TenantCreatedDetails(AuditPlan Plan) : AuditDetails;
