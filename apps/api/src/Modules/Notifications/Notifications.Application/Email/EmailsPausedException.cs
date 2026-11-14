namespace Notifications.Application.Email;

/// <summary>The kill switch <c>emails.send</c> is off: the email waits, and its message is scheduled again (<see cref="EmailSwitch"/>).</summary>
public sealed class EmailsPausedException : Exception
{
    public EmailsPausedException()
        : base("The kill switch emails.send is off, so the email waits until it is on again.")
    {
    }

    public EmailsPausedException(string message)
        : base(message)
    {
    }

    public EmailsPausedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
