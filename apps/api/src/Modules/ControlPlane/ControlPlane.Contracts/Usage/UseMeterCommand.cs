using SharedKernel;

namespace ControlPlane.Contracts.Usage;

/// <summary>
/// A product's module asks whether the tenant of the message may use a meter by this quantity, and counts the use (step 40c,
/// docs/flows/plans-and-entitlements.md). It travels in the tenant (T2), invoked inline; the answer is <see cref="MeterUse"/>. The key is a
/// meter that the code defines (<c>MeterCatalog</c>); the quantity is at least 1.
/// </summary>
[MessageName("use-meter")]
public sealed record UseMeterCommand(string MeterKey, long Quantity);
