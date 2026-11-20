namespace ControlPlane.Application.Tenants.GetBillingSituations;

/// <summary>The tenants of the situation asked for, or null for no filter, and the count of each situation.</summary>
public sealed record BillingSituationsView(IReadOnlyList<Guid>? Only, int PaymentOverdue, int Contract);
