namespace ControlPlane.Infrastructure.Clerk;

// Clerk's Backend API (section ControlPlane:Clerk), checked on start. The secret key comes from the environment only, never from a
// committed file.
internal sealed class ClerkOptions
{
    public const string Section = "ControlPlane:Clerk";

    public string? SecretKey { get; set; }

    // The signing secret of the webhook endpoint, from the Clerk dashboard. Only a process that serves the API needs it.
    public string? WebhookSigningSecret { get; set; }

    public Uri BackendApiUrl { get; set; } = new("https://api.clerk.com/v1/");

    // How long a call may take.
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
