using SharedKernel;

namespace Billing.Application.PriceChanges.AnnouncePriceChange;

/// <summary>
/// Tells each tenant that pays another price for the plan of the change that it moves to the new one (B13), 30 days before the move. It
/// travels without a tenant, as a plan belongs to none, and sends one message for each tenant to tell, in that tenant (W3).
/// </summary>
[MessageName("announce-price-change")]
public sealed record AnnouncePriceChangeCommand(Guid PriceChangeId);
