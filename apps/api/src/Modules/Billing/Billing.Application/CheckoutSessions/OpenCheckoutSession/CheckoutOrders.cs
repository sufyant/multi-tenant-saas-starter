using Billing.Application.CheckoutSessions.GetCheckoutFacts;
using Billing.Application.Subscriptions.GetBilling;
using Billing.Application.Subscriptions;
using Billing.Domain.Subscriptions;
using SharedKernel;

namespace Billing.Application.CheckoutSessions.OpenCheckoutSession;

/// <summary>
/// The rules of a checkout, before the provider's page opens: billing is on, the page to come back to is the web app's, the tenant does not
/// pay by card already (it changes its plan instead), and the plan costs something for its seated members. The page says how the person
/// came back: <c>checkout=success</c> or <c>checkout=cancelled</c>. Every rejection comes first.
/// </summary>
public static class CheckoutOrders
{
    public const string ReturnParameter = "checkout";

    public static Result<OpenCheckoutSessionQuery> Of(
        BillingOptions billing,
        Guid tenantId,
        CheckoutFacts facts,
        SubscriptionView? subscription,
        string interval,
        Uri returnUrl)
    {
        if (!billing.Accepts(returnUrl))
        {
            return Errors.ReturnUrlInvalid;
        }

        if (subscription is not null && !Subscription.HasEnded(Enum.Parse<SubscriptionStatus>(subscription.Status)))
        {
            return Errors.AlreadySubscribed;
        }

        var yearly = Enum.Parse<BillingInterval>(interval) == BillingInterval.Yearly;
        var (baseAmount, perPerson) = yearly ? (facts.BaseYearly, facts.PerPersonYearly) : (facts.BaseMonthly, facts.PerPersonMonthly);
        if (baseAmount == 0 && (perPerson == 0 || facts.SeatedMembers == 0))
        {
            return Errors.PlanFree;
        }

        return new OpenCheckoutSessionQuery(
            Key: null,
            tenantId, facts.PaidBy, CustomerId: null, facts.PlanId, facts.PlanName, facts.Currency, interval, baseAmount, perPerson,
            facts.SeatedMembers, new Uri($"{returnUrl}?{ReturnParameter}=success"), new Uri($"{returnUrl}?{ReturnParameter}=cancelled"));
    }
}
