using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Admin;

// GET /v1/admin/tenants: every tenant, in every status, for the system admin. The list reads across tenants, so each test has a
// database of its own, where no other test adds a tenant.
public sealed class ListTenantsTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Catalog _catalog = null!;
    private ApiFactory _api = null!;

    public async ValueTask InitializeAsync()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        _catalog = new Catalog(database, name);
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, name));
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ListTenants_TenantsInEveryStatus_ReturnsEachByNameThenId()
    {
        var random = Guid.NewGuid().ToString();
        var delta = await _catalog.AddTenantAsync(name: "Delta Ltd");
        var alpha = await _catalog.AddTenantAsync(status: "Provisioning", name: "Alpha Ltd");
        var charlie = await _catalog.AddTenantAsync(status: "Suspended", name: "Charlie Ltd");
        var bravo = await _catalog.AddTenantAsync(status: "Cancelled", name: "Bravo Ltd");
        var echoLater = await _catalog.AddTenantAsync(name: "Echo Ltd", tenantId: Guid.Parse($"f{random[1..]}"));
        var echoEarlier = await _catalog.AddTenantAsync(name: "Echo Ltd", tenantId: Guid.Parse($"0{random[1..]}"));

        var response = await (await AdminAsync()).GetAsync("/v1/admin/tenants", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Tenants(page).ShouldBe(
        [
            (alpha.Id, "Alpha Ltd", alpha.Slug, "Provisioning"),
            (bravo.Id, "Bravo Ltd", bravo.Slug, "Cancelled"),
            (charlie.Id, "Charlie Ltd", charlie.Slug, "Suspended"),
            (delta.Id, "Delta Ltd", delta.Slug, "Active"),
            (echoEarlier.Id, "Echo Ltd", echoEarlier.Slug, "Active"),
            (echoLater.Id, "Echo Ltd", echoLater.Slug, "Active"),
        ]);
        page.GetProperty("items")[0].EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status"], ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(50);
        page.GetProperty("total").GetInt32().ShouldBe(6);
    }

    [Fact]
    public async Task ListTenants_ALaterPage_ReturnsTheRestAndTheTotal()
    {
        await _catalog.AddTenantAsync(name: "First Ltd");
        await _catalog.AddTenantAsync(name: "Second Ltd");
        var third = await _catalog.AddTenantAsync(name: "Third Ltd");

        var page = await (await AdminAsync()).GetFromJsonAsync<JsonElement>("/v1/admin/tenants?page=2&pageSize=2", Cancellation);

        Tenants(page).ShouldBe([(third.Id, "Third Ltd", third.Slug, "Active")]);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("pageSize").GetInt32().ShouldBe(2);
        page.GetProperty("total").GetInt32().ShouldBe(3);
    }

    [Theory]
    [InlineData("pageSize=101", "paging.page_size_invalid")]
    [InlineData("page=0", "paging.page_invalid")]
    public async Task ListTenants_PagingOutsideItsRange_IsRejected(string query, string code)
    {
        var response = await (await AdminAsync()).GetAsync($"/v1/admin/tenants?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe(code);
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private static List<(Guid Id, string Name, string Slug, string Status)> Tenants(JsonElement page) =>
    [
        .. page.GetProperty("items").EnumerateArray().Select(item => (
            item.GetProperty("id").GetGuid(),
            item.GetProperty("name").GetString()!,
            item.GetProperty("slug").GetString()!,
            item.GetProperty("status").GetString()!)),
    ];
}
