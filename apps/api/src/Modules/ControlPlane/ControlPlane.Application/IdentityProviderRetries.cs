using ControlPlane.Application.Invitations.SignUp;
using ControlPlane.Application.Users.UserDeletion;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace ControlPlane.Application;

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

    public bool IsValid => Delays.Count > 0 && Delays[0] > TimeSpan.Zero && Delays.Zip(Delays.Skip(1)).All(pair => pair.Second > pair.First);
}

// A call to the identity provider that fails is tried again after each configured pause; then the message goes to the dead letter
// queue. Each of these messages has one handler, so a retry may be scheduled (W5). A pause of a minute or more is a scheduled retry: the
// message goes back to its queue with the time of its next try, so it holds no worker meanwhile. A shorter pause is waited out where the
// handler runs.
internal sealed class IdentityProviderRetries(IOptions<IdentityProviderOptions> settings) : IWolverineExtension, IHandlerPolicy
{
    private static readonly TimeSpan LongestInlinePause = TimeSpan.FromMinutes(1);

    private static readonly Type[] Calls = [typeof(OpenOwnerSignUp), typeof(OpenMemberSignUp), typeof(WithdrawSignUp), typeof(DeleteIdentityAccount)];

    public void Configure(WolverineOptions options) => options.Policies.Add(this);

    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var inline = settings.Value.Delays.TakeWhile(delay => delay < LongestInlinePause).ToArray();
        var scheduled = settings.Value.Delays.Skip(inline.Length).ToArray();
        foreach (var chain in chains.Where(chain => Calls.Contains(chain.MessageType)))
        {
            var retries = chain.OnAnyException();
            var next = inline.Length > 0 ? retries.RetryWithCooldown(inline).Then : retries;
            if (scheduled.Length > 0)
            {
                next.ScheduleRetry(scheduled);
            }
        }
    }
}
