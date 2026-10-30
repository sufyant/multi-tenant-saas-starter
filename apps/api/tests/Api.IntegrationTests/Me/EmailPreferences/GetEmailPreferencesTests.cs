using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Me.EmailPreferences;

// GET /v1/me/email-preferences (U5): the main switch and the categories that can reach the user, from their permissions in every tenant
// and whether they are a system admin. Every category's email is off by default today.
public sealed class GetEmailPreferencesTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    public static TheoryData<string, string[]> CategoriesByRole => new()
    {
        { "Member", ["access"] },
        { "Admin", ["access", "member_invitations"] },
        { "Owner", ["access", "member_invitations", "tenant_plans"] },
    };

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [MemberData(nameof(CategoriesByRole))]
    public async Task GetEmailPreferences_AMemberOfOneTenant_ListsTheCategoriesTheirRoleCanHearAllOff(string role, string[] categories)
    {
        var member = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, role: role);

        var preferences = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>("/v1/me/email-preferences", TestContext.Current.CancellationToken);

        preferences.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        preferences.GetProperty("categories").EnumerateArray().Select(category => (category.GetProperty("category").GetString()!, category.GetProperty("enabled").GetBoolean()))
            .ShouldBe(categories.Select(category => (category, false)));
    }

    [Fact]
    public async Task GetEmailPreferences_AMemberHereAndAnOwnerThereWhoIsASystemAdmin_ListsEveryCategory()
    {
        var admin = await _testData.AddSystemAdminAsync();
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, admin);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, admin, role: "Owner");

        var preferences = await _api.CreateClient(admin).GetFromJsonAsync<JsonElement>("/v1/me/email-preferences", TestContext.Current.CancellationToken);

        preferences.GetProperty("categories").EnumerateArray().Select(category => category.GetProperty("category").GetString())
            .ShouldBe(["access", "member_invitations", "tenant_plans", "platform_tenants"]);
    }
}
