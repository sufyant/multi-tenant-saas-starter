using SharedKernel;

namespace ControlPlane.Application.Identity;

/// <summary>
/// Configuration of the calls to the identity provider (section <c>ControlPlane:Clerk</c>), checked on start. The retries of a call
/// grow as the invitation email's do, because the provider may be down for a while.
/// </summary>
public sealed class IdentityProviderOptions
{
    public const string Section = "ControlPlane:Clerk";

    private static readonly TimeSpan[] DefaultRetryDelays =
        [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    // Unset unless configured, so configured pauses replace the defaults: the binder would add them to an array it finds.
    public TimeSpan[]? RetryDelays { get; set; }

    internal IReadOnlyList<TimeSpan> Delays => RetryDelays ?? DefaultRetryDelays;

    public bool IsValid => GrowingRetries.AreValid(Delays);
}
