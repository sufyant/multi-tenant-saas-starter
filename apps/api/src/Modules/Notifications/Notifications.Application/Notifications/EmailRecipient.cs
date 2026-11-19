using SharedKernel;

namespace Notifications.Application.Notifications;

/// <summary>An address that gets a notification by email only, with no account and no bell (B14): its language and time zone.</summary>
public sealed record EmailRecipient(string Email, Language Language, string? TimeZone);
