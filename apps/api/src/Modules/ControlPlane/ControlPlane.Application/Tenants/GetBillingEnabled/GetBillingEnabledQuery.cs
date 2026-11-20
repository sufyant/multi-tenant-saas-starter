using SharedKernel;

namespace ControlPlane.Application.Tenants.GetBillingEnabled;

/// <summary>Whether the deployment's billing is on (B1), for <c>GetMyTenant</c>, which every member reads.</summary>
[MessageName("get-billing-enabled")]
public sealed record GetBillingEnabledQuery;
