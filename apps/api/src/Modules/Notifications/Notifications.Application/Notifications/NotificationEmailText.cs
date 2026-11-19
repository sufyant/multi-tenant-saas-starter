namespace Notifications.Application.Notifications;

/// <summary>A notification as its email writes it: the title, the body of the email and the label of its button.</summary>
public sealed record NotificationEmailText(string Title, string Body, string Button);
