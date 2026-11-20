using SharedKernel;

namespace ControlPlane.Application.Usage.GetUsage;

/// <summary>
/// The use of the meters of the tenant's plan, for Usage (11.01) and the platform's Usage tab (4.37). It runs inside the tenant: the tenant
/// path's or the platform route's (W3).
/// </summary>
[MessageName("get-usage")]
public sealed record GetUsageQuery;
