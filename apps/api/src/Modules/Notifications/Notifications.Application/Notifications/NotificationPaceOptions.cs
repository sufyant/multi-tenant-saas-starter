namespace Notifications.Application.Notifications;

// How many deliveries a fan-out to every user sends each second (section Notifications:Pace), validated at startup by the module's
// infrastructure. Each delivery may send one email, and the email service limits the requests of the whole team each second: Resend's
// default is 10 (Resend, "Rate limit"). Half of it leaves room for the invitation, code and other emails of the same seconds.
public sealed class NotificationPaceOptions
{
    public const string Section = "Notifications:Pace";

    public int DeliveriesPerSecond { get; set; } = 5;

    public bool IsValid => DeliveriesPerSecond >= 1;
}
