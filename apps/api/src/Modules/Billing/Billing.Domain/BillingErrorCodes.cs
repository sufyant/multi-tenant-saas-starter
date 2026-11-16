using SharedKernel;

namespace Billing.Domain;

/// <summary>The codes of every error Billing answers, in its lowest layer, so that every layer of the module reaches them (K5).</summary>
public static class BillingErrorCodes
{
    public static readonly ErrorCode BillingOff = ErrorCode.Declare("billing.off");

    public static readonly ErrorCode BillingPlanFree = ErrorCode.Declare("billing.plan_free");

    public static readonly ErrorCode BillingPlanNotFound = ErrorCode.Declare("billing.plan_not_found");

    public static readonly ErrorCode BillingNoCustomer = ErrorCode.Declare("billing.no_customer");

    public static readonly ErrorCode BillingAlreadySubscribed = ErrorCode.Declare("billing.already_subscribed");

    public static readonly ErrorCode BillingReturnUrlInvalid = ErrorCode.Declare("billing.return_url_invalid");

    public static readonly ErrorCode BillingCheckoutReplaced = ErrorCode.Declare("billing.checkout_replaced");

    public static readonly ErrorCode BillingTenantClosed = ErrorCode.Declare("billing.tenant_closed");

    public static readonly ErrorCode BillingNoSubscription = ErrorCode.Declare("billing.no_subscription");

    public static readonly ErrorCode BillingSubscriptionNotActive = ErrorCode.Declare("billing.subscription_not_active");

    public static readonly ErrorCode BillingPlanUnchanged = ErrorCode.Declare("billing.plan_unchanged");

    public static readonly ErrorCode BillingNothingScheduled = ErrorCode.Declare("billing.nothing_scheduled");

    public static readonly ErrorCode BillingPaymentDeclined = ErrorCode.Declare("billing.payment_declined");

    public static readonly ErrorCode BillingPreviewExpired = ErrorCode.Declare("billing.preview_expired");

    public static readonly ErrorCode BillingWebhookInvalid = ErrorCode.Declare("billing.webhook_invalid");

    public static readonly ErrorCode BillingContract = ErrorCode.Declare("billing.contract");

    public static readonly ErrorCode BillingTenantNotFound = ErrorCode.Declare("billing.tenant_not_found");

    public static readonly ErrorCode BillingNothingToMove = ErrorCode.Declare("billing.nothing_to_move");
}
