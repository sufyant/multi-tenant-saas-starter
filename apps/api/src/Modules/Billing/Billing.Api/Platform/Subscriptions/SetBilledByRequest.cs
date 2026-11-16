namespace Billing.Api.Platform.Subscriptions;

/// <summary>A system admin's switch of how the tenant pays (4.35, "Switch to Contract · Switch to Card").</summary>
public sealed record SetBilledByRequest(SetBilledByRequestBilledBy BilledBy);
