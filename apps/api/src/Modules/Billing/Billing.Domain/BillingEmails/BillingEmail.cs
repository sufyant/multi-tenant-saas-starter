using SharedKernel;

namespace Billing.Domain.BillingEmails;

/// <summary>
/// The tenant's optional billing email (10.01, B14): an address that needs no account. The provider's invoices go to it, as the customer's
/// email, and billing's payment emails go to it besides the members who manage billing. It keeps the member who set it last, whose language
/// and time zone its emails take. One row for each tenant that has one, under the tenant's row level security; a tenant that never paid by
/// card has no customer, so the address cannot live on the customer's row.
/// </summary>
internal sealed class BillingEmail(string address, Guid setBy, DateTimeOffset setAt) : ITenantEntity
{
    /// <summary>The longest address the app keeps (RFC 5321, section 4.5.3.1.3: a path of 256 octets, an address of 254).</summary>
    public const int MaxLength = 254;

    public string Address { get; private set; } = address;

    /// <summary>The user id of the member who set the address last.</summary>
    public Guid SetBy { get; private set; } = setBy;

    public DateTimeOffset SetAt { get; private set; } = setAt;

    /// <summary>Takes another address; the same address changes nothing.</summary>
    public StateChange Change(string address, Guid setBy, DateTimeOffset at)
    {
        if (string.Equals(Address, address, StringComparison.OrdinalIgnoreCase))
        {
            return StateChange.Unchanged;
        }

        (Address, SetBy, SetAt) = (address, setBy, at);
        return StateChange.Changed;
    }
}
