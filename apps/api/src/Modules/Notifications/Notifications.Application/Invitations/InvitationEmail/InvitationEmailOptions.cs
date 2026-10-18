using SharedKernel;

namespace Notifications.Application.Invitations.InvitationEmail;

// Configuration of the invitation email (section Notifications), validated at startup by its infrastructure.
internal sealed class InvitationEmailOptions
{
    public const string Section = "Notifications";

    private static readonly TimeSpan[] DefaultInvitationEmailRetryDelays =
        [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    // The growing pauses before each new try of the invitation email; the email service may be down for a while. Unset unless
    // configured, so configured pauses replace the defaults: the binder would add them to an array it finds.
    public TimeSpan[]? InvitationEmailRetryDelays { get; set; }

    public IReadOnlyList<TimeSpan> RetryDelays => InvitationEmailRetryDelays ?? DefaultInvitationEmailRetryDelays;

    public bool IsValid => GrowingRetries.AreValid(RetryDelays);
}
