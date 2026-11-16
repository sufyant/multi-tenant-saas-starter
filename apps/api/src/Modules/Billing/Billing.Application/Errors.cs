using Billing.Domain;
using SharedKernel;

namespace Billing.Application;

/// <summary>The errors that several features of Billing answer, and its API layer too.</summary>
public static class Errors
{
    public static readonly Error BillingOff = Error.NotFound(BillingErrorCodes.BillingOff, "Billing is off in this deployment.");

    public static readonly Error PlanNotFound = Error.NotFound(BillingErrorCodes.BillingPlanNotFound, "The plan was not found.");

    public static readonly Error PlanFree = Error.Unprocessable(BillingErrorCodes.BillingPlanFree, "The plan costs nothing, so it has no checkout.");

    public static readonly Error AlreadySubscribed = Error.Conflict(BillingErrorCodes.BillingAlreadySubscribed, "The organisation pays by card already; it changes its plan instead.");

    public static readonly Error NoCustomer = Error.Conflict(BillingErrorCodes.BillingNoCustomer, "The organisation has not paid by card yet, so it has no billing account.");

    public static readonly Error ReturnUrlInvalid = Error.Validation(BillingErrorCodes.BillingReturnUrlInvalid, "The address to come back to is not a page of the web app.");

    public static readonly Error CheckoutReplaced = Error.Conflict(BillingErrorCodes.BillingCheckoutReplaced, "Another checkout replaced this one while it opened. Open the checkout again.");

    public static readonly Error TenantClosed = Error.Conflict(BillingErrorCodes.BillingTenantClosed, "The organisation is closed, so it pays for nothing.");

    public static readonly Error NoSubscription = Error.Conflict(BillingErrorCodes.BillingNoSubscription, "The organisation does not pay by card; it opens a checkout instead.");

    public static readonly Error SubscriptionNotActive = Error.Conflict(BillingErrorCodes.BillingSubscriptionNotActive, "The subscription is not active, so its plan cannot change now.");

    public static readonly Error PlanUnchanged = Error.Conflict(BillingErrorCodes.BillingPlanUnchanged, "The organisation is on this plan and interval already, or its cancellation is asked already.");

    public static readonly Error NothingScheduled = Error.Conflict(BillingErrorCodes.BillingNothingScheduled, "No plan change or cancellation is scheduled.");

    public static readonly Error PaymentDeclined = Error.Unprocessable(BillingErrorCodes.BillingPaymentDeclined, "The card was declined, so the plan did not change.");

    public static readonly Error PreviewExpired = Error.Validation(BillingErrorCodes.BillingPreviewExpired, "The preview is too old or from the future. Ask for a new one.");

    public static readonly Error Contract = Error.Conflict(BillingErrorCodes.BillingContract, "The organisation is billed outside the app by invoice. To change its plan, send a request.");

    public static readonly Error TenantNotFound = Error.NotFound(BillingErrorCodes.BillingTenantNotFound, "The organisation was not found.");

    public static readonly Error WebhookInvalid = Error.Validation(BillingErrorCodes.BillingWebhookInvalid, "The event's signature is missing, wrong or too old.");
}
