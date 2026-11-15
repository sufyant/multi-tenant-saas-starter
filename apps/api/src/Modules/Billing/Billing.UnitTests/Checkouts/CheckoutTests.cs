using Billing.Domain.Checkouts;

namespace Billing.UnitTests.Checkouts;

// B7: the same order keeps its key, so every call of it to Stripe is the same call; another order, a new price, or a key that waited too long
// for its page takes a new key, and the replaced one is named, so its pages are expired.
public sealed class CheckoutTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly CheckoutOrderOf Monthly = new(Guid.CreateVersion7(), false, 3, "https://app.test/acme/settings/billing", "EUR", 900, 1000);

    [Fact]
    public void Reserve_TheSameOrderWhileItsPageHasTime_KeepsTheKey()
    {
        var checkout = Checkout.Open(Guid.CreateVersion7(), Monthly, At);
        checkout.Record(checkout.Key, "cs_1", "https://checkout.stripe.test/cs_1");

        var replaced = checkout.Reserve(Guid.CreateVersion7(), Monthly, At.AddMinutes(49));

        (replaced, checkout.Url).ShouldBe((null, "https://checkout.stripe.test/cs_1"));
    }

    // A new price of the plan names other prices at Stripe, so the same key would be another call.
    [Fact]
    public void Reserve_ANewPrice_TakesANewKeyAndNamesTheReplacedOne()
    {
        var checkout = Checkout.Open(Guid.CreateVersion7(), Monthly, At);
        var first = checkout.Key;

        var replaced = checkout.Reserve(Guid.CreateVersion7(), Monthly with { PerPersonAmount = 1200 }, At.AddMinutes(1));

        replaced.ShouldBe(first);
        checkout.Key.ShouldNotBe(first);
    }

    // Stripe wants a page's expiry at least 30 minutes after the call that makes it, and the expiry is fixed at the reservation.
    [Fact]
    public void Reserve_AKeyWithoutAPageAfterTheFirstCallsWindow_TakesANewKey()
    {
        var checkout = Checkout.Open(Guid.CreateVersion7(), Monthly, At);
        var first = checkout.Key;

        var replaced = checkout.Reserve(Guid.CreateVersion7(), Monthly, At.AddMinutes(21));

        replaced.ShouldBe(first);
        checkout.ExpiresAt.ShouldBe(At.AddMinutes(21) + Checkout.Lifetime);
    }

    [Fact]
    public void Reserve_APageNearItsExpiry_TakesANewKey()
    {
        var checkout = Checkout.Open(Guid.CreateVersion7(), Monthly, At);
        checkout.Record(checkout.Key, "cs_1", "https://checkout.stripe.test/cs_1");

        var replaced = checkout.Reserve(Guid.CreateVersion7(), Monthly, At.AddMinutes(51));

        replaced.ShouldNotBeNull();
        checkout.Url.ShouldBeNull();
    }

    [Fact]
    public void Record_APageOfAReplacedKey_KeepsNothing()
    {
        var checkout = Checkout.Open(Guid.CreateVersion7(), Monthly, At);
        var first = checkout.Key;
        checkout.Reserve(Guid.CreateVersion7(), Monthly with { Yearly = true }, At.AddMinutes(1));

        var kept = checkout.Record(first, "cs_1", "https://checkout.stripe.test/cs_1");

        (kept, checkout.Url).ShouldBe((false, null));
    }
}
