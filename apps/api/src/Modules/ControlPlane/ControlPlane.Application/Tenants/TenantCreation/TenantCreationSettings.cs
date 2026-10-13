namespace ControlPlane.Application.Tenants.TenantCreation;

/// <summary>
/// Configuration of tenant creation (section <c>ControlPlane</c>), validated at startup; public because a handler takes it as a
/// parameter.
/// </summary>
public sealed class TenantCreationSettings
{
    public const string Section = "ControlPlane";

    /// <summary>From the start of the tenant creation until the tenant is active; then the tenant creation needs attention.</summary>
    public TimeSpan ActivationTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// From the tenant's activation until the invitation email is sent; then the tenant creation needs attention. Longer than the email
    /// retries Notifications is configured with, so it only fires when a message is lost. If it is not, the invitation is cancelled
    /// and the alarm is raised. The setup notes say this.
    /// </summary>
    public TimeSpan InvitationEmailTimeout { get; set; } = TimeSpan.FromHours(2);

    /// <summary>From the start of a cancellation until the tenant is cancelled; then the tenant creation needs attention.</summary>
    public TimeSpan CancellationTimeout { get; set; } = TimeSpan.FromMinutes(10);

    internal TenantCreationTimeouts Timeouts => new(ActivationTimeout, InvitationEmailTimeout, CancellationTimeout);
}
