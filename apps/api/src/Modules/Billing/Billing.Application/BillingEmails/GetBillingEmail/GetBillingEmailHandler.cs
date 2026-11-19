namespace Billing.Application.BillingEmails.GetBillingEmail;

// Row level security keeps the read to the tenant that the transaction declares (R1).
public static class GetBillingEmailHandler
{
    public static Task<BillingEmailView?> HandleAsync(GetBillingEmailQuery query, IBillingEmailRepository emails, CancellationToken cancellationToken) =>
        emails.ReadAsync(cancellationToken);
}
