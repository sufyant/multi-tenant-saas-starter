namespace Notifications.Application.Email;

/// <summary>
/// An email to one address, in HTML and in plain text: a generated template of <c>packages/emails</c>, filled (<see cref="EmailTemplate"/>).
/// The email service sends one email per idempotency key, so the same email asked for twice goes out once (O4).
/// </summary>
public sealed record EmailMessage(string To, string Subject, string Html, string Text, string IdempotencyKey);
