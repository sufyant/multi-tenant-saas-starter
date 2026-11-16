namespace Billing.Application.Contracts.GetContract;

// Row level security keeps the read to the tenant that the transaction declares (R1).
public static class GetContractHandler
{
    public static Task<bool> HandleAsync(GetContractQuery query, IContractRepository contracts, CancellationToken cancellationToken) =>
        contracts.IsContractAsync(cancellationToken);
}
