namespace Billing.Domain.Subscriptions;

/// <summary>
/// What our app asked the provider about a subscription's end because of the tenant's closure (B8): nothing, the end at the period's end
/// while the tenant is closed or Contract (B12), or the undo of that end after a reopen or a return to card, until the provider's state
/// shows it. A change of the provider's
/// state that disagrees asks again, so the provider ends in what we asked, whatever order our calls reached it in.
/// </summary>
internal enum ClosureEnd
{
    None,
    Asked,
    Released,
}
