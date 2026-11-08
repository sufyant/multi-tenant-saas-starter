using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// The latest Owner invitation of a tenant that has no Owner: its address, its expiry, its state at the moment of the request,
/// ControlPlane's name of it (<c>Pending</c>, <c>Expired</c>, <c>Cancelled</c>), the language it keeps for its email (K8), and when it
/// was sent, null for a summary saved before the column.
/// </summary>
public sealed record OwnerInvitationView(string Email, DateTimeOffset ExpiresAt, string State, Language Language, DateTimeOffset? SentAt)
{
    internal static OwnerInvitationView? Of(TenantSummary summary, DateTimeOffset now) =>
        summary.OwnerInvitationStateAt(now) is { } state
            ? new(
                summary.OwnerInvitationEmail!,
                summary.OwnerInvitationExpiresAt!.Value,
                state.ToString(),
                summary.OwnerInvitationLanguage!.Value,
                summary.OwnerInvitationSentAt)
            : null;
}
