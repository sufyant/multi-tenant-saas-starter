using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Admin;

// GET /v1/admin/tenants/{tenantId}: one tenant, in every status, for the system admin.
public sealed class GetTenantTests(Database database) : IAsyncLifetime
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
    [InlineData("Provisioning", "provisioning")]
    [InlineData("Active", "active")]
    [InlineData("Suspended", "suspended")]
    [InlineData("Cancelled", "cancelled")]
    public async Task GetTenant_ATenantInAnyStatus_ReturnsItWithItsStatus(string stored, string answered)
    {
        var tenant = await _catalog.AddTenantAsync(status: stored, name: "Admin View Ltd");

        var response = await (await AdminAsync()).GetAsync($"/v1/admin/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var found = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (found.GetProperty("id").GetGuid(), found.GetProperty("name").GetString(), found.GetProperty("slug").GetString(), found.GetProperty("status").GetString())
            .ShouldBe((tenant.Id, "Admin View Ltd", tenant.Slug, answered));
        found.EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetTenant_AnIdNoTenantHas_IsNotFound()
    {
        var response = await (await AdminAsync()).GetAsync($"/v1/admin/tenants/{Guid.NewGuid()}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("tenant.not_found");
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);
}
