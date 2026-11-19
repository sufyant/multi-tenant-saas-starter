using SharedKernel;

namespace ControlPlane.Application.Tenants.GetBillingSituations;

/// <summary>
/// What the Organisations list reads from Billing (4.38; B11, B12): the tenants of one billing situation, when the list filters by one
/// (its name, as <c>Statuses</c> of <c>ListTenantsQuery</c> names a status), and the count of each. A query of its own, as one handler
/// has one DbContext (W1).
/// </summary>
[MessageName("get-billing-situations")]
public sealed record GetBillingSituationsQuery(string? Situation);
