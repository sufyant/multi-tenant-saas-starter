using SharedKernel;

namespace Billing.Application.Contracts.GetContract;

/// <summary>Whether the tenant of the transaction is Contract (B12), without a lock: true while a system admin's mark holds.</summary>
[MessageName("get-contract")]
public sealed record GetContractQuery;
