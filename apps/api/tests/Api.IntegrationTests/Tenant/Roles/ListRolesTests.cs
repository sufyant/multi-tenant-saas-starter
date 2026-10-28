using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Roles;

// GET /v1/tenants/{tenantId}/roles: the tenant's roles, built in and custom, each with its id, its name, whether it is built in and its
// permissions, in the list contract of K5. Every built-in role that invites or changes a member's role reads them (decision 1 of step
// r4b).
public sealed class ListRolesTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    public async Task ListRoles_AsAnOwnerOrAnAdmin_AnswersTheBuiltInAndTheCustomRolesByName(string role)
    {
        var tenant = await _testData.AddTenantAsync();
        var actor = await _testData.AddMemberAsync(tenant.Id, role: role);
        var billing = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read", "tenant.audit.read");

        var page = await _api.CreateClient(actor).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/roles", Cancellation);

        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => $"{item.GetProperty("name").GetString()}:{item.GetProperty("builtIn").GetBoolean()}")
            .ShouldBe(["admin:True", "Billing:False", "member:True", "owner:True"]);
        items[1].GetProperty("id").GetGuid().ShouldBe(billing);
        items[1].GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()).ShouldBe(["tenant.audit.read", "tenant.members.read"]);
        items[2].GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()).ShouldBe(["tenant.members.read", "tenant.profile.read"]);
        items[2].GetProperty("id").GetGuid().ShouldBe(await _testData.RoleIdOfAsync(tenant.Id, "Member"));
        page.GetProperty("total").GetInt32().ShouldBe(4);
        items[0].EnumerateObject().Select(field => field.Name)
            .ShouldBe(["id", "name", "builtIn", "permissions", "memberCount", "pendingInvitationCount"], ignoreOrder: true);
    }

    // The counts follow the rule of role.in_use: every membership with the role, and every pending invitation that gives it, an expired
    // one included. A finished invitation counts nothing. So the screen disables "Delete role" exactly when DeleteRole refuses it.
    [Fact]
    public async Task ListRoles_ARoleWithAMemberAndPendingInvitations_CountsThemAsDeleteRoleDoes()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var billing = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");
        await _testData.AddMemberWithRoleAsync(tenant.Id, billing);
        await _testData.AddInvitationAsync(tenant.Id, roleId: billing);
        await _testData.AddInvitationAsync(tenant.Id, roleId: billing, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
        await _testData.AddInvitationAsync(tenant.Id, roleId: billing, status: "Cancelled");
        await _testData.AddInvitationAsync(tenant.Id, roleId: billing, status: "Accepted");

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/roles?q=billing", Cancellation);

        var role = page.GetProperty("items")[0];
        role.GetProperty("memberCount").GetInt32().ShouldBe(1);
        role.GetProperty("pendingInvitationCount").GetInt32().ShouldBe(2);
    }

    // A role that only an expired pending invitation gives counts one invitation, and DeleteRole refuses it: both read one rule.
    [Fact]
    public async Task ListRoles_ARoleOnlyAnExpiredInvitationGives_CountsItAndDeleteRoleRefusesIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var billing = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");
        await _testData.AddInvitationAsync(tenant.Id, roleId: billing, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/roles?q=billing", Cancellation);
        var deleted = await _api.CreateClient(owner).DeleteRoleAsync(tenant.Id, billing);

        var role = page.GetProperty("items")[0];
        role.GetProperty("memberCount").GetInt32().ShouldBe(0);
        role.GetProperty("pendingInvitationCount").GetInt32().ShouldBe(1);
        deleted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // A built-in role counts its members too; another tenant's members and invitations never count (R1).
    [Fact]
    public async Task ListRoles_ABuiltInRole_CountsOnlyThisTenantsMembersAndInvitations()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.AddMemberAsync(tenant.Id);
        await _testData.AddMemberAsync(tenant.Id);
        await _testData.AddInvitationAsync(tenant.Id);
        var other = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(other.Id);
        await _testData.AddInvitationAsync(other.Id);

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/roles?q=member", Cancellation);

        var member = page.GetProperty("items")[0];
        member.GetProperty("memberCount").GetInt32().ShouldBe(2);
        member.GetProperty("pendingInvitationCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task ListRoles_ASearchAndADescendingSort_AnswersTheMatchingRolesInThatOrder()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.AddCustomRoleAsync(tenant.Id, "Billing reader", "tenant.members.read");
        await _testData.AddCustomRoleAsync(tenant.Id, "Billing writer", "tenant.members.read");

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/roles?q=billing&sort=-name", Cancellation);

        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()).ShouldBe(["Billing writer", "Billing reader"]);
    }

    // A Member gives no role, so it does not read them.
    [Fact]
    public async Task ListRoles_AsAMember_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);

        var listed = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/roles", Cancellation);

        listed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Row level security keeps another tenant's roles out of the list.
    [Fact]
    public async Task ListRoles_AnotherTenantHasACustomRole_LeavesItOut()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var other = await _testData.AddTenantAsync();
        await _testData.AddCustomRoleAsync(other.Id, "Elsewhere", "tenant.members.read");

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/roles", Cancellation);

        page.GetProperty("total").GetInt32().ShouldBe(3);
    }

    // The page is one query, with each role's permissions read with it: a longer page sends no more commands (K5).
    [Fact]
    public async Task ListRoles_ALongerPage_SendsNoMoreSqlCommands()
    {
        using var commands = new SqlCommands();
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read", "tenant.audit.read");
        var url = $"/v1/tenants/{tenant.Id}/roles";

        var one = await commands.CountAsync(() => _api.CreateClient(owner), client => client.GetAsync($"{url}?pageSize=1", Cancellation));
        var three = await commands.CountAsync(() => _api.CreateClient(owner), client => client.GetAsync($"{url}?pageSize=3", Cancellation));

        three.ShouldBe(one);
        one.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ListRoles_FeatureOff_ListsTheCustomRolesToo()
    {
        var (tenantId, owner) = await PlanAnswers.FreeTenantAsync(_testData, _api);
        await _testData.AddCustomRoleAsync(tenantId, "Billing", "tenant.profile.read");

        var listed = await owner.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenantId}/roles", Cancellation);

        listed.GetProperty("total").GetInt32().ShouldBe(4);
    }
}
