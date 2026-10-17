using System.Net;
using Tenancy;

namespace Api.IntegrationTests.Tenant;

// T3 and T7: a caller cannot tell "not a member" from "no such tenant". The tenant door answers both with the handler's own
// tenant.not_found, so neither its status nor its body, apart from the trace id, says which one it was.
public sealed class TenantNotFoundTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task EnterTenant_NotAMemberOrNoSuchTenant_AnswersTheHandlersTenantNotFound()
    {
        var tenant = await _catalog.AddTenantAsync();
        var outsider = _api.CreateClient(await _catalog.AddUserAsync());
        var admin = _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

        var notAMember = await outsider.GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);
        var noSuchTenant = await outsider.GetAsync($"/v1/tenants/{Guid.NewGuid()}/members", Cancellation);
        var fromTheHandler = await admin.GetAsync($"/v1/platform/tenants/{Guid.NewGuid()}", Cancellation);

        (notAMember.StatusCode, noSuchTenant.StatusCode, fromTheHandler.StatusCode)
            .ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        var handlers = await ProblemBodies.WithoutTraceIdAsync(fromTheHandler);
        handlers.ShouldContain("\"code\":\"tenant.not_found\"");
        (await ProblemBodies.WithoutTraceIdAsync(notAMember)).ShouldBe(handlers);
        (await ProblemBodies.WithoutTraceIdAsync(noSuchTenant)).ShouldBe(handlers);
    }
}
