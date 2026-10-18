using ControlPlane.Contracts;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.Extensions.Options;
using SharedKernel;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Notifications.Application.Invitations.InvitationEmail;

// An invitation email that fails is tried again after each configured pause, which grows; then the message goes to the dead letter
// queue. For a tenant's first owner the tenant creation saga hears of it; a member's invitation stays pending. Both emails have one
// handler each, so a retry may be scheduled (W5). The handler holds no transaction while it waits. GrowingRetries splits the pauses: a
// pause of a minute or more is a scheduled retry, so the event goes back to its queue with the time of its next try and holds no worker
// meanwhile, and a shorter pause is waited out where the handler runs.
internal sealed class InvitationEmailRetries(IOptions<InvitationEmailOptions> settings) : IWolverineExtension, IHandlerPolicy
{
    public void Configure(WolverineOptions options) => options.Policies.Add(this);

    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var (inline, scheduled) = GrowingRetries.Of(settings.Value.RetryDelays);
        foreach (var chain in chains.Where(chain => chain.MessageType == typeof(OwnerInvitationReady) || chain.MessageType == typeof(MemberInvitationReady)))
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
