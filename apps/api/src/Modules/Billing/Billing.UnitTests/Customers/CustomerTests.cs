using Billing.Domain.Customers;

namespace Billing.UnitTests.Customers;

// B8: the tenant's closures and reopens arrive in no order, so billing takes each only when it is newer than the last one it took.
public sealed class CustomerTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Close_ThenAReopenOfLater_OpensIt()
    {
        var customer = new Customer("cus_1", Guid.CreateVersion7(), Guid.CreateVersion7(), At);
        customer.Close(At.AddMinutes(1));

        var taken = customer.Reopen(At.AddMinutes(2));

        (taken, customer.ClosedAt).ShouldBe((true, null));
    }

    // The reopen came first; the closure before it arrives late and changes nothing.
    [Fact]
    public void Close_OlderThanTheReopenTaken_ChangesNothing()
    {
        var customer = new Customer("cus_1", Guid.CreateVersion7(), Guid.CreateVersion7(), At);
        customer.Close(At.AddMinutes(1));
        customer.Reopen(At.AddMinutes(3));

        var taken = customer.Close(At.AddMinutes(2));

        (taken, customer.ClosedAt).ShouldBe((false, null));
    }

    [Fact]
    public void Reopen_OlderThanTheClosureTaken_ChangesNothing()
    {
        var customer = new Customer("cus_1", Guid.CreateVersion7(), Guid.CreateVersion7(), At);
        customer.Close(At.AddMinutes(3));

        var taken = customer.Reopen(At.AddMinutes(2));

        (taken, customer.ClosedAt).ShouldBe((false, At.AddMinutes(3)));
    }
}
