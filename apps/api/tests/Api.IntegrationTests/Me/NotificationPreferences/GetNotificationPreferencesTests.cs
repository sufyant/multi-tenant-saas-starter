using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Me.NotificationPreferences;

// GET /v1/me/notification-preferences (U5): a table whose columns are the channels, each with its main switch, and whose rows are the
// categories that can reach the user, from their permissions in every tenant and whether they are a system admin. The required category,
// account_security, is listed to everyone, on in every channel. Each row names its types in the reader's language. The defaults today: the
// web on and the email off.
public sealed class GetNotificationPreferencesTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    public static TheoryData<string, string[]> CategoriesByRole => new()
    {
        { "Member", ["account_security required web=True email=True", "access web=True email=False"] },
        { "Admin", ["account_security required web=True email=True", "access web=True email=False", "member_invitations web=True email=False"] },
        {
            "Owner",
            [
                "account_security required web=True email=True",
                "access web=True email=False",
                "member_invitations web=True email=False",
                "tenant_plans web=True email=False",
            ]
        },
    };

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [MemberData(nameof(CategoriesByRole))]
    public async Task GetNotificationPreferences_AMemberOfOneTenant_ListsTheCategoriesTheirRoleCanHearWithTheDefaults(string role, string[] rows)
    {
        var member = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, role: role);

        var preferences = await ReadAsync(member);

        Channels(preferences.GetProperty("channels")).ShouldBe("web=True email=True");
        Rows(preferences).ShouldBe(rows);
    }

    [Fact]
    public async Task GetNotificationPreferences_AMemberHereAndAnOwnerThereWhoIsASystemAdmin_ListsEveryCategory()
    {
        var admin = await _testData.AddSystemAdminAsync();
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, admin);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, admin, role: "Owner");

        var preferences = await ReadAsync(admin);

        preferences.GetProperty("categories").EnumerateArray().Select(category => category.GetProperty("category").GetString())
            .ShouldBe(["account_security", "access", "member_invitations", "tenant_plans", "platform_tenants"]);
    }

    [Fact]
    public async Task GetNotificationPreferences_AReaderInTurkish_NamesEachCategorysTypesInTurkish()
    {
        var member = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, await _testData.AddUserAsync(language: "tr"));

        var preferences = await ReadAsync(member);

        preferences.GetProperty("categories").EnumerateArray()
            .Select(category => $"{category.GetProperty("category").GetString()}: {string.Join(", ", category.GetProperty("types").EnumerateArray().Select(type => $"{type.GetProperty("type").GetString()} {type.GetProperty("name").GetString()}"))}")
            .ShouldBe(["account_security: member.removed Bir kuruluştan çıkarıldınız", "access: member.role_changed Rolünüz değişti"]);
    }

    // Each row as "category[ required] web=… email=…".
    private static IEnumerable<string> Rows(JsonElement preferences) =>
        preferences.GetProperty("categories").EnumerateArray().Select(category =>
            string.Join(' ', new[] { category.GetProperty("category").GetString()!, category.GetProperty("required").GetBoolean() ? "required" : "", Channels(category.GetProperty("channels")) }.Where(part => part.Length > 0)));

    private static string Channels(JsonElement channels) =>
        string.Join(' ', channels.EnumerateArray().Select(channel => $"{channel.GetProperty("channel").GetString()}={channel.GetProperty("enabled").GetBoolean()}"));

    private async Task<JsonElement> ReadAsync(string user) =>
        await _api.CreateClient(user).GetFromJsonAsync<JsonElement>("/v1/me/notification-preferences", TestContext.Current.CancellationToken);
}
