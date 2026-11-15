using Billing.Api.Tenant.Subscriptions;

namespace Billing.Api.Statuses;

/// <summary>Maps each of Billing's kinds to the enum of a response (N7r). A kind a response cannot carry is a fault, not an answer.</summary>
internal static class ResponseStatus
{
    public static SubscriptionResponseInterval OfInterval(string interval) => interval switch
    {
        "Monthly" => SubscriptionResponseInterval.Monthly,
        "Yearly" => SubscriptionResponseInterval.Yearly,
        _ => throw Unknown(interval),
    };

    public static PaymentResponseState OfPayment(string state) => state switch
    {
        "Retrying" => PaymentResponseState.Retrying,
        "Grace" => PaymentResponseState.Grace,
        "MovedToDefault" => PaymentResponseState.MovedToDefault,
        _ => throw Unknown(state),
    };

    public static SubscriptionResponseStatus OfSubscription(string status) => status switch
    {
        "Incomplete" => SubscriptionResponseStatus.Incomplete,
        "IncompleteExpired" => SubscriptionResponseStatus.IncompleteExpired,
        "Trialing" => SubscriptionResponseStatus.Trialing,
        "Active" => SubscriptionResponseStatus.Active,
        "PastDue" => SubscriptionResponseStatus.PastDue,
        "Canceled" => SubscriptionResponseStatus.Canceled,
        "Unpaid" => SubscriptionResponseStatus.Unpaid,
        "Paused" => SubscriptionResponseStatus.Paused,
        _ => throw Unknown(status),
    };

    private static ArgumentOutOfRangeException Unknown(string kind) =>
        new(nameof(kind), kind, "The response has no value for this kind of Billing.");
}
