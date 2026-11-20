namespace ControlPlane.Application.Usage;

/// <summary>The reads of a tenant's use of its meters. The tenant is the transaction's (R4).</summary>
/// <remarks>Public because a public handler takes it (W9); its members are internal to the module.</remarks>
public interface IUsageQueries
{
    /// <summary>
    /// The tenant's use in the month of <paramref name="period"/>, and in up to <paramref name="pastMonths"/> months before it, from the
    /// month the tenant was created; none when no tenant is declared.
    /// </summary>
    internal Task<UsageView?> FindUsageAsync(DateOnly period, int pastMonths, CancellationToken cancellationToken);
}
