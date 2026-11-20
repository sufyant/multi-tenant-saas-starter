using SharedKernel;

namespace ControlPlane.Application.Usage.SetSpendingCap;

/// <summary>
/// A member who manages billing sets the tenant's spending cap, the most that the extra of its charge meters may cost in a month, in minor
/// units excl. VAT, or removes it with none (11.02, 11.03).
/// </summary>
[MessageName("set-spending-cap")]
public sealed record SetSpendingCapCommand(string ActorId, long? Amount);
