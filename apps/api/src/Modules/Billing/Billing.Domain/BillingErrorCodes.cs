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

    public static readonly ErrorCode BillingWebhookInvalid = ErrorCode.Declare("billing.webhook_invalid");
}
