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

    public static readonly Error WebhookInvalid = Error.Validation(BillingErrorCodes.BillingWebhookInvalid, "The event's signature is missing, wrong or too old.");
}
