using SharedKernel;

namespace Notifications.Application.Invitations;

// Configuration of the invitation email (section Notifications:InvitationEmails), validated at startup by its infrastructure.
internal sealed class InvitationEmailOptions
{
    public const string Section = "Notifications:InvitationEmails";

    private static readonly TimeSpan[] DefaultRetryDelays =
        [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    // The growing pauses before each new try of the invitation email; the email service may be down for a while. Unset unless
    // configured, so configured pauses replace the defaults: the binder would add them to an array it finds.
    public TimeSpan[]? RetryDelays { get; set; }

    public IReadOnlyList<TimeSpan> Delays => RetryDelays ?? DefaultRetryDelays;

    public bool IsValid => GrowingRetries.AreValid(Delays);
}
