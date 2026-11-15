using Billing.Application.CheckoutSessions.GetCheckoutFacts;
using Billing.Application.CheckoutSessions.OpenCheckoutSession;
using Billing.Application.Subscriptions;
using Billing.Application.Subscriptions.GetBilling;

namespace Billing.UnitTests.CheckoutSessions.OpenCheckoutSession;

// The rules of a checkout before Stripe's page opens (step 40): the page to come back to is the web app's, a tenant that pays by card
// changes its plan instead, and the plan must cost something for its seated members.
public sealed class CheckoutOrdersTests
{
    private static readonly BillingOptions Billing = new() { Enabled = true, WebUrl = new Uri("https://app.test/") };

    private static readonly Uri ReturnUrl = new("https://app.test/acme/settings/billing");

    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public void Of_AMonthlyPlan_OrdersTheMonthlyPricesForTheSeatedMembers()
    {
        var order = CheckoutOrders.Of(Billing, TenantId, Facts(baseMonthly: 900, perPersonMonthly: 1000, seated: 3), null, "Monthly", ReturnUrl);

        (order.Value.Interval, order.Value.BaseAmount, order.Value.PerPersonAmount, order.Value.People).ShouldBe(("Monthly", 900L, 1000L, 3));
        order.Value.SuccessUrl.ShouldBe(new Uri("https://app.test/acme/settings/billing?checkout=success"));
    }

    [Fact]
    public void Of_AYearlyPlan_OrdersTheYearlyPrices()
    {
        var order = CheckoutOrders.Of(Billing, TenantId, Facts(baseYearly: 9000, perPersonYearly: 10000, seated: 2), null, "Yearly", ReturnUrl);

        (order.Value.BaseAmount, order.Value.PerPersonAmount).ShouldBe((9000L, 10000L));
    }

    // Only a fee per person, and nobody who takes a seat: the order would be empty.
    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(0, 1000, 0)]
    public void Of_NothingToPay_IsThePlanFree(long baseMonthly, long perPersonMonthly, int seated)
    {
        var order = CheckoutOrders.Of(Billing, TenantId, Facts(baseMonthly: baseMonthly, perPersonMonthly: perPersonMonthly, seated: seated), null, "Monthly", ReturnUrl);

        order.Error.Code.Value.ShouldBe("billing.plan_free");
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("PastDue")]
    [InlineData("Incomplete")]
    public void Of_ASubscriptionThatHasNotEnded_IsAlreadySubscribed(string status)
    {
        var order = CheckoutOrders.Of(Billing, TenantId, Facts(baseMonthly: 900), Subscription(status), "Monthly", ReturnUrl);

        order.Error.Code.Value.ShouldBe("billing.already_subscribed");
    }

    // A subscription that ended lets the tenant pay again, on its own customer.
    [Theory]
    [InlineData("Canceled")]
    [InlineData("IncompleteExpired")]
    public void Of_ASubscriptionThatEnded_OrdersOnTheSameCustomer(string status)
    {
        var order = CheckoutOrders.Of(Billing, TenantId, Facts(baseMonthly: 900), Subscription(status), "Monthly", ReturnUrl);

        order.Value.CustomerId.ShouldBe("cus_1");
    }

    [Theory]
    [InlineData("https://evil.test/acme/settings/billing")]
    [InlineData("http://app.test/acme/settings/billing")]
    [InlineData("https://app.test:8443/acme/settings/billing")]
    [InlineData("https://app.test/acme/settings/billing?next=https://evil.test")]
    [InlineData("https://app.test/acme/settings/billing#top")]
    public void Of_AnAddressOutsideTheWebApp_IsRefused(string returnUrl)
    {
        var order = CheckoutOrders.Of(Billing, TenantId, Facts(baseMonthly: 900), null, "Monthly", new Uri(returnUrl));

        order.Error.Code.Value.ShouldBe("billing.return_url_invalid");
    }

    // A web app under a path accepts only addresses under that path.
    [Fact]
    public void Of_AWebAppUnderAPath_RefusesAnAddressBesideIt()
    {
        var billing = new BillingOptions { Enabled = true, WebUrl = new Uri("https://example.test/app/") };

        var order = CheckoutOrders.Of(billing, TenantId, Facts(baseMonthly: 900), null, "Monthly", new Uri("https://example.test/other/billing"));

        order.Error.Code.Value.ShouldBe("billing.return_url_invalid");
    }

    private static CheckoutFacts Facts(long baseMonthly = 0, long perPersonMonthly = 0, long baseYearly = 0, long perPersonYearly = 0, int seated = 1) =>
        new(Guid.CreateVersion7(), "ada@example.test", Guid.CreateVersion7(), "Team", false, "EUR", baseMonthly, perPersonMonthly, baseYearly, perPersonYearly, Guid.CreateVersion7(), seated);

    private static SubscriptionView Subscription(string status) =>
        new("cus_1", Guid.CreateVersion7(), "Monthly", status, DateTimeOffset.UnixEpoch, false);
}
