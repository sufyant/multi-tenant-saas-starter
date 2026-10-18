using SharedKernel;

namespace Notifications.Contracts.Invitations;

/// <summary>
/// The reply to the tenant creation saga: the invitation email went to the email service (N3, S13). Sent through the outbox, in the
/// invitation's tenant; only the saga handles it.
/// </summary>
/// <param name="EventId">Chosen by the sender, so a saga that receives the reply twice can tell.</param>
[MessageName("send-invitation-email-reply")]
public sealed record SendInvitationEmailReply(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid InvitationId);
