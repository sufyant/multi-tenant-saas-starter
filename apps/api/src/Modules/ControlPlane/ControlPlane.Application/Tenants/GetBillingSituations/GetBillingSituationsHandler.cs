using Billing.Contracts.Tenants;

namespace ControlPlane.Application.Tenants.GetBillingSituations;

// Reads Billing through its contract, across tenants (R6). While billing is off, it answers null: no situation filters or counts.
public static class GetBillingSituationsHandler
{
    public static async Task<BillingSituationsView?> HandleAsync(
        GetBillingSituationsQuery query,
        IBillingDirectory billing,
        CancellationToken cancellationToken)
    {
        if (await billing.CountAsync(cancellationToken) is not { } counts)
        {
            return null;
        }

        var only = query.Situation is { } situation
            ? await billing.ListTenantIdsAsync(Enum.Parse<BillingSituation>(situation), cancellationToken)
            : null;
        return new BillingSituationsView(only, counts.PaymentOverdue, counts.Contract);
    }
}
