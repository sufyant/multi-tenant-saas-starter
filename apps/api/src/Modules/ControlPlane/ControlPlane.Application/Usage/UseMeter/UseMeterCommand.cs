using ControlPlane.Contracts.Usage;
using SharedKernel;

namespace ControlPlane.Application.Usage.UseMeter;

/// <summary>
/// Whether the tenant of the message may use a meter by this quantity, and the count of the use (step 40c,
/// docs/flows/plans-and-entitlements.md). <see cref="IUsageMeter"/> invokes it inline, in the tenant (T2); the answer is
/// <see cref="MeterUse"/>. The key is a meter that the code defines (<c>MeterCode</c>); the quantity is at least 1.
/// </summary>
[MessageName("use-meter")]
public sealed record UseMeterCommand(string MeterKey, long Quantity);
