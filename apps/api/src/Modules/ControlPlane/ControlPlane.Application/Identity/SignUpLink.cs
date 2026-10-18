namespace ControlPlane.Application.Identity;

/// <summary>
/// The link an invitation email carries, and the provider's invitation behind it. <see cref="InvitationId"/> is null when the email
/// already has an account; <see cref="Link"/> is then the accept link.
/// </summary>
public sealed record SignUpLink(Uri Link, string? InvitationId);
