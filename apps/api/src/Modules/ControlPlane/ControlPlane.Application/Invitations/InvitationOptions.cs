namespace ControlPlane.Application.Invitations;

/// <summary>
/// Configuration of invitations (section <c>ControlPlane:Invitations</c>), checked on start; public because a handler takes it (W9).
/// </summary>
public sealed class InvitationOptions
{
    public const string Section = "ControlPlane:Invitations";

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How many invitations a tenant may create in any 24 hours, cancelled and replaced ones included (K2).</summary>
    public int DailyLimitPerTenant { get; set; } = 50;

    /// <summary>The client page where a signed-in person accepts an invitation; the invitation code is added as the <c>code</c> parameter.</summary>
    public Uri? AcceptUrl { get; set; }

    internal Uri AcceptLink(InvitationCode code) => new UriBuilder(AcceptUrl!) { Query = $"code={Uri.EscapeDataString(code.ToString())}" }.Uri;
}
