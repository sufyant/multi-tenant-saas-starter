using ControlPlane.Application.Invitations.OpenMemberSignUp;
using ControlPlane.Application.Invitations.OpenOwnerSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Users.DeleteIdentityAccount;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.Extensions.Options;
using SharedKernel;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace ControlPlane.Application.Identity;

// A call to the identity provider that fails is tried again after each configured pause; then the message goes to the dead letter
// queue. Each of these messages has one handler, so a retry may be scheduled (W5). GrowingRetries splits the pauses: a pause of a minute
// or more is a scheduled retry, so the message holds no worker meanwhile, and a shorter pause is waited out where the handler runs.
internal sealed class IdentityProviderRetries(IOptions<IdentityProviderOptions> settings) : IWolverineExtension, IHandlerPolicy
{
    private static readonly Type[] Calls = [typeof(OpenOwnerSignUpCommand), typeof(OpenMemberSignUpCommand), typeof(WithdrawSignUpCommand), typeof(DeleteIdentityAccountCommand)];

    public void Configure(WolverineOptions options) => options.Policies.Add(this);

    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var (inline, scheduled) = GrowingRetries.Of(settings.Value.Delays);
        foreach (var chain in chains.Where(chain => Calls.Contains(chain.MessageType)))
        {
            var retries = chain.OnAnyException();
            var next = inline.Count > 0 ? retries.RetryWithCooldown([.. inline]).Then : retries;
            if (scheduled.Count > 0)
            {
                next.ScheduleRetry([.. scheduled]);
            }
        }
    }
}
