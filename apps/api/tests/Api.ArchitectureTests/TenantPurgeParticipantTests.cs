using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Runtime.Handlers;

namespace Api.ArchitectureTests;

// docs/flows/tenant-purge.md: the purge waits for a reply from each module the host names as a participant, and knows no module. A module
// that handles PurgeTenantDataCommand and is not named would keep its data while the purge ends; a module named without a handler would
// keep the purge waiting until its alarm. So the two lists are the same. Read from Wolverine's own handler graph of the application
// composed as a worker, and from its services.
public sealed class TenantPurgeParticipantTests : IAsyncLifetime
{
    private readonly ComposedApplication _application = new("worker");

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _application.DisposeAsync();

    [Fact]
    public void ComposeTheParticipants_EveryModuleThatPurgesATenantsData_IsNamedOnceAndNoOtherIs()
    {
        var graph = _application.Services.GetRequiredService<HandlerGraph>();
        var handling = graph.Chains
            .Concat(graph.Chains.SelectMany(chain => chain.ByEndpoint))
            .Where(chain => chain.MessageType == typeof(PurgeTenantDataCommand))
            .SelectMany(chain => chain.Handlers)
            .Select(call => call.HandlerType.Assembly.GetName().Name!.Split('.')[0])
            .Distinct();

        var named = _application.Services.GetServices<TenantPurgeParticipant>().Select(participant => participant.Module).ToList();

        named.ShouldBe(handling, ignoreOrder: true);
        named.ShouldBe(["Audit", "Notifications"], ignoreOrder: true);
    }
}
