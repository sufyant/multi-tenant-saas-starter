using ControlPlane.Application.Identity;

namespace ControlPlane.Infrastructure.Clerk;

// Clerk's Backend API (section ControlPlane:Clerk), checked on start. The secret key comes from the environment only, never from a
// committed file.
internal sealed class ClerkOptions
{
    public const string Section = IdentityProviderOptions.Section;

    public string? SecretKey { get; set; }

    public Uri BackendApiUrl { get; set; } = new("https://api.clerk.com/v1/");

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
