using ControlPlane.Application.DataExports.DeleteStoredFile;
using ControlPlane.Application.DataExports.GetDownloadLink;
using ControlPlane.Application.DataExports.PrepareDataExport;
using ControlPlane.Application.Identity;
using ControlPlane.Application.Invitations.OpenSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Users.DeleteIdentityAccount;
using ControlPlane.Application.Users.DeleteUserFiles;
using ControlPlane.Application.Users.GetEmailTakenAtIdentityProvider;
using ControlPlane.Application.Users.SendIdentityEmail;
using ControlPlane.Contracts.Invitations;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using JasperFx;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Email;
using Notifications.Application.Notifications.SendNotificationEmail;
using SharedKernel;
using Wolverine.Runtime.Handlers;

namespace Api.ArchitectureTests;

// Section 3, Handlers and pipeline: a handler that calls an external service uses no DbContext, so no transaction is open during
// the call (Nygard, Chapter 5). Read from Wolverine's own handler graph of the composed application: no chain that reaches the
// identity provider, the email channel or the file store has Wolverine's EF Core transaction.
public sealed class ExternalCallTransactionTests : IAsyncLifetime
{
    // The tag Wolverine's EF Core integration puts on a chain whose transaction it owns (EFCorePersistenceFrameProvider, internal).
    private const string EfCoreTransaction = "uses_efcore_transaction";

    private static readonly Type[] ExternalServices = [typeof(IIdentityProvider), typeof(IEmailChannel), typeof(IFileStore)];

    private readonly ComposedApplication _application = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _application.DisposeAsync();

    [Fact]
    public void ReadTheHandlerGraph_EveryChainThatCallsAnExternalService_HasNoEfCoreTransaction()
    {
        var graph = _application.Services.GetRequiredService<HandlerGraph>();
        var container = _application.Services.GetRequiredService<IServiceContainer>();
        HandlerChain[] chains = [.. graph.Chains.Concat(graph.Chains.SelectMany(chain => chain.ByEndpoint)).Where(chain => chain.Handlers.Count > 0)];

        var calling = chains.Where(chain => ServicesOf(chain).Any(service => Reaches(service, container, []))).ToList();
        var withTransaction = calling.Where(chain => chain.Tags.ContainsKey(EfCoreTransaction)).Select(Describe);

        calling.Select(chain => chain.MessageType).ShouldBe(
            [
                typeof(OpenSignUpCommand), typeof(WithdrawSignUpCommand), typeof(DeleteIdentityAccountCommand), typeof(SendIdentityEmailCommand), typeof(GetEmailTakenAtIdentityProviderQuery),
                typeof(InvitationReadyEvent),
                typeof(TenantPurgeWarningReadyEvent),
                typeof(SendNotificationEmailCommand),
                typeof(VerificationCodeReadyEvent), typeof(EmailChangeNoticeReadyEvent), typeof(UserDeletionConfirmationReadyEvent),
                typeof(PrepareDataExportCommand), typeof(DeleteStoredFileCommand), typeof(GetDownloadLinkQuery), typeof(DeleteUserFilesCommand),
                typeof(PurgeTenantDataCommand),
            ],
            ignoreOrder: true);
        withTransaction.ShouldBeEmpty();
    }

    // What Wolverine passes to the handler: its parameters, and for a handler that is not static, the handler itself.
    private static IEnumerable<Type> ServicesOf(HandlerChain chain) =>
        chain.Handlers.SelectMany(call => call.Method.GetParameters().Select(parameter => parameter.ParameterType).Append(call.HandlerType));

    // Whether the service is an external service, or is built on one.
    private static bool Reaches(Type service, IServiceContainer container, HashSet<Type> seen) =>
        ExternalServices.Contains(service)
        || (seen.Add(service) && container.ServiceDependenciesFor(service).Any(dependency => Reaches(dependency, container, seen)));

    private static string Describe(HandlerChain chain) =>
        $"{chain.MessageType.Name} -> {string.Join(", ", chain.Handlers.Select(call => call.HandlerType.Name))}";
}
