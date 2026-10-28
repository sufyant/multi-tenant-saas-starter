namespace SharedKernel;

/// <summary>
/// Endpoint metadata: this tenant endpoint needs no feature of the tenant's plan, on purpose. Every tenant endpoint states either the
/// feature it needs, as an authorization policy, or this mark (docs/flows/plans-and-entitlements.md), so an ungated endpoint is a choice,
/// never a forgotten line. A limit, and a feature that depends on the request's body, is checked by the handler under the tenant's lock.
/// </summary>
public sealed class NotPlanGated;
