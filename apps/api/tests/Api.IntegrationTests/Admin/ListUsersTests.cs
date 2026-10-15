using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Admin;

// GET /v1/admin/users: every catalog user, for the system admin, by email address, then id. Users belong to no tenant, so each test
// has a database of its own, where no other test adds a user.
public sealed class ListUsersTests(Database database) : IAsyncLifetime
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

    // A user without an email address, such as one the identity provider deleted, comes last. A deleted user stays deleted when they
    // were suspended too.
    [Fact]
    public async Task ListUsers_UsersInEveryStatus_ReturnsEachByEmailThenId()
    {
        var random = Guid.NewGuid().ToString();
        var carol = await _catalog.AddUserAsync(email: "carol@example.com", firstName: "Carol", lastName: "Shaw");
        var bob = await _catalog.AddSystemAdminAsync(email: "bob@example.com");
        var alice = await _catalog.AddUserAsync(email: "alice@example.com", firstName: "Alice", lastName: "Ball");
        await _catalog.SuspendUserAsync(alice);
        var deletedLater = await _catalog.AddUserAsync(Guid.Parse($"f{random[1..]}"), email: "dave@example.com");
        await _catalog.DeleteUserAsync(deletedLater);
        await _catalog.SuspendUserAsync(deletedLater);
        var deletedEarlier = await _catalog.AddUserAsync(Guid.Parse($"0{random[1..]}"), email: "erin@example.com");
        await _catalog.DeleteUserAsync(deletedEarlier);

        var response = await _api.CreateClient(bob, secondFactor: true).GetAsync("/v1/admin/users", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Users(page).ShouldBe(
        [
            (await _catalog.UserIdOfAsync(alice), "alice@example.com", "Alice", "Ball", "suspended", false),
            (await _catalog.UserIdOfAsync(bob), "bob@example.com", null, null, "active", true),
            (await _catalog.UserIdOfAsync(carol), "carol@example.com", "Carol", "Shaw", "active", false),
            (Guid.Parse($"0{random[1..]}"), null, null, null, "deleted", false),
            (Guid.Parse($"f{random[1..]}"), null, null, null, "deleted", false),
        ]);
        page.GetProperty("items")[0].EnumerateObject().Select(field => field.Name)
            .ShouldBe(["id", "email", "firstName", "lastName", "status", "isSystemAdmin"], ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(50);
        page.GetProperty("total").GetInt32().ShouldBe(5);
    }

    [Fact]
    public async Task ListUsers_ALaterPage_ReturnsTheRestAndTheTotal()
    {
        var admin = await _catalog.AddSystemAdminAsync(email: "admin@example.com");
        await _catalog.AddUserAsync(email: "bea@example.com");
        var last = await _catalog.AddUserAsync(email: "cem@example.com");

        var page = await _api.CreateClient(admin, secondFactor: true).GetFromJsonAsync<JsonElement>("/v1/admin/users?page=2&pageSize=2", Cancellation);

        Users(page).ShouldBe([(await _catalog.UserIdOfAsync(last), "cem@example.com", null, null, "active", false)]);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("total").GetInt32().ShouldBe(3);
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    public async Task ListUsers_PagingOutsideItsRange_IsABadRequest(string query)
    {
        var admin = await _catalog.AddSystemAdminAsync();

        var response = await _api.CreateClient(admin, secondFactor: true).GetAsync($"/v1/admin/users?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static List<(Guid Id, string? Email, string? FirstName, string? LastName, string Status, bool IsSystemAdmin)> Users(JsonElement page) =>
    [
        .. page.GetProperty("items").EnumerateArray().Select(item => (
            item.GetProperty("id").GetGuid(),
            item.GetProperty("email").GetString(),
            item.GetProperty("firstName").GetString(),
            item.GetProperty("lastName").GetString(),
            item.GetProperty("status").GetString()!,
            item.GetProperty("isSystemAdmin").GetBoolean())),
    ];
}
