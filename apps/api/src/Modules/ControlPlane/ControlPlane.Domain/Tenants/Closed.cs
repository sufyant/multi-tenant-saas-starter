using SharedKernel;

namespace ControlPlane.Domain.Tenants;

/// <summary>What <see cref="Tenant.Close"/> did: whether the status changed, and the pending invitations it cancelled.</summary>
internal sealed record Closed(StateChange Change, IReadOnlyList<Invitation> Cancelled);
