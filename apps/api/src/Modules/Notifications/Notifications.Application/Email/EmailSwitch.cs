using ControlPlane.Contracts.FeatureFlags;

namespace Notifications.Application.Email;

/// <summary>
/// The kill switch of every email, the template's flag <c>emails.send</c> (docs/flows/feature-flags.md, section 8). While it is off for
/// everyone else, an email is not sent: its message throws <see cref="EmailsPausedException"/>, which Wolverine schedules again every
/// <see cref="Pause"/> for as long as it takes (<see cref="Invitations.InvitationEmailRetries"/>), so no email is lost and each goes once
/// the flag is on again (the maintainer's decision of 2026-11-14). The email's message stays in the durable store meanwhile (W5).
/// </summary>
/// <remarks>Public because a public handler takes it (W9).</remarks>
public sealed class EmailSwitch(IFeatureFlags flags)
{
    /// <summary>How long a paused email waits before it asks the flag again.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromMinutes(1);

    /// <summary>Sends the email while the switch is on; otherwise throws, and the message waits.</summary>
    public async Task SendAsync(IEmailChannel channel, EmailMessage email, CancellationToken cancellationToken)
    {
        if (!await flags.IsOnForEveryoneElseAsync(FeatureFlagKeys.EmailsSend, cancellationToken))
        {
            throw new EmailsPausedException();
        }

        await channel.SendAsync(email, cancellationToken);
    }
}
