using ControlPlane.Contracts.Invitations;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using JasperFx;
using Notifications.Application.Notifications.SendNotificationEmail;
using JasperFx.CodeGeneration;
using Microsoft.Extensions.Options;
using SharedKernel;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Notifications.Application.Invitations;

// An invitation email that fails is tried again after each configured pause, which grows; then the message goes to the dead letter
// queue, and the invitation stays pending. The warning of a tenant's purge and the emails about a person's own account are of the same kind, so they take the same retries, and so does the generic notification email: a fan-out to every user can meet the email service's rate limit (429), and the email then goes later (U3, step 38). The email has one handler, so a retry may be scheduled (W5). The handler holds no transaction while it waits. GrowingRetries splits the pauses: a
// pause of a minute or more is a scheduled retry, so the event goes back to its queue with the time of its next try and holds no worker
// meanwhile, and a shorter pause is waited out where the handler runs.
internal sealed class InvitationEmailRetries(IOptions<InvitationEmailOptions> settings) : IWolverineExtension, IHandlerPolicy
{
    private static readonly Type[] Emails =
    [
        typeof(InvitationReadyEvent),
        typeof(TenantPurgeWarningReadyEvent),
        typeof(VerificationCodeReadyEvent),
        typeof(EmailChangeNoticeReadyEvent),
        typeof(UserDeletionConfirmationReadyEvent),
        typeof(SendNotificationEmailCommand),
    ];

    public void Configure(WolverineOptions options) => options.Policies.Add(this);

    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var (inline, scheduled) = GrowingRetries.Of(settings.Value.Delays);
        foreach (var chain in chains.Where(chain => Emails.Contains(chain.MessageType)))
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
