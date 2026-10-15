using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Tenant;

// GET /v1/tenants/{tenantId}: the tenant in the route, for its members, in the shape of an item of their tenant list. It passes the
// tenant door as every tenant route does.
public sealed class GetMyTenantTests(Database database) : IAsyncLifetime
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

    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("Member")]
    public async Task GetMyTenant_AMemberOfEveryBuiltInRole_ReturnsTheTenant(string role)
    {
        var tenant = await _catalog.AddTenantAsync(name: "Profile Ltd");
        var member = await _catalog.AddMemberAsync(tenant.Id, role: role);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var found = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (found.GetProperty("id").GetGuid(), found.GetProperty("name").GetString(), found.GetProperty("slug").GetString(), found.GetProperty("status").GetString())
            .ShouldBe((tenant.Id, "Profile Ltd", tenant.Slug, "active"));
        found.EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status"], ignoreOrder: true);
    }

    // T3: a tenant the caller cannot enter looks the same as one that does not exist.
    [Fact]
    public async Task GetMyTenant_AsAUserWhoIsNotAMember_IsNotFound()
    {
        var tenant = await _catalog.AddTenantAsync();
        await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var outsider = await _catalog.AddMemberAsync((await _catalog.AddTenantAsync()).Id, role: "Owner");

        var response = await _api.CreateClient(outsider).GetAsync($"/v1/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyTenant_UnknownTenantId_IsNotFound()
    {
        var user = await _catalog.AddUserAsync();

        var response = await _api.CreateClient(user).GetAsync($"/v1/tenants/{Guid.NewGuid()}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyTenant_ASuspendedTenant_IsForbiddenAndToldWhy()
    {
        var tenant = await _catalog.AddTenantAsync(status: "Suspended");
        var member = await _catalog.AddMemberAsync(tenant.Id);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("tenant.suspended");
    }
}
