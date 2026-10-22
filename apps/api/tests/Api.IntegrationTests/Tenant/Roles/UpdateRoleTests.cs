using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Roles;

// PATCH /v1/tenants/{tenantId}/roles/{roleId}: a member whose role holds tenant.roles.update gives a custom role a name and exactly these
// permissions (A3). A built-in role cannot be changed (A2). The actor's role holds every permission of the role before and after
// (decision 2 of step r4b). It sets a state, so the name and the permissions the role has answer 200 and record nothing (I1).
public sealed class UpdateRoleTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task UpdateRole_ANewNameAndPermissions_ChangesThemAndRecordsWhatChanged()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read", "tenant.invitations.read");

        var updated = await _api.WaitingForMessagesAsync(() =>
            _api.CreateClient(owner).UpdateRoleAsync(tenant.Id, roleId, " Finance ", ["tenant.members.read", "tenant.audit.read"]));

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await updated.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (body.GetProperty("id").GetGuid(), body.GetProperty("name").GetString(), body.GetProperty("builtIn").GetBoolean()).ShouldBe((roleId, "Finance", false));
        body.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()).ShouldBe(["tenant.audit.read", "tenant.members.read"]);
        (await PermissionsOfAsync(roleId)).ShouldBe("tenant.audit.read, tenant.members.read");
        (await _audit.TenantLogAsync(roleId)).ShouldBe($"{tenant.Id}:role.updated|User|{await _testData.UserIdOfAsync(owner)}|Role|{roleId}|Finance");
        (await _audit.TenantDetailsAsync(roleId))
            .ShouldBe("""{"newName": "Finance", "oldName": "Billing", "permissionsAdded": ["tenant.audit.read"], "permissionsRemoved": ["tenant.invitations.read"]}""");
    }

    [Fact]
    public async Task UpdateRole_TheNameAndPermissionsItHas_AnswersTheRoleAndRecordsNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");

        var updated = await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).UpdateRoleAsync(tenant.Id, roleId, "Billing", ["tenant.members.read"]));

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _audit.TenantLogAsync(roleId)).ShouldBeNull();
    }

    [Fact]
    public async Task UpdateRole_ByAnAdmin_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await _testData.AddMemberAsync(tenant.Id, role: "Admin");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");

        var updated = await _api.CreateClient(admin).UpdateRoleAsync(tenant.Id, roleId, "Finance", ["tenant.members.read"]);

        updated.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await NameOfAsync(roleId)).ShouldBe("Billing");
    }

    // A2: the built-in roles keep their names and their permissions in code.
    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("Member")]
    public async Task UpdateRole_ABuiltInRole_IsAConflictAndChangesNothing(string builtIn)
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.RoleIdOfAsync(tenant.Id, builtIn);

        var updated = await _api.CreateClient(owner).UpdateRoleAsync(tenant.Id, roleId, "Renamed", ["tenant.members.read"]);

        updated.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(updated)).ShouldBe("role.built_in");
        (await PermissionsOfAsync(roleId)).ShouldBeNull();
    }

    // Row level security hides another tenant's roles, so its id is not found.
    [Fact]
    public async Task UpdateRole_ARoleOfAnotherTenant_IsNotFoundAndChangesNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var other = await _testData.AddTenantAsync();
        var roleId = await _testData.AddCustomRoleAsync(other.Id, "Billing", "tenant.members.read");

        var updated = await _api.CreateClient(owner).UpdateRoleAsync(tenant.Id, roleId, "Finance", ["tenant.members.read"]);

        updated.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(updated)).ShouldBe("role.not_found");
        (await NameOfAsync(roleId)).ShouldBe("Billing");
    }

    [Fact]
    public async Task UpdateRole_ANameAnotherRoleHas_IsAConflict()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");

        var updated = await _api.CreateClient(owner).UpdateRoleAsync(tenant.Id, roleId, "Admin", ["tenant.members.read"]);

        updated.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(updated)).ShouldBe("role.name_taken");
    }

    // A role may take its own name in another case.
    [Fact]
    public async Task UpdateRole_ItsOwnNameInAnotherCase_ChangesIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");

        var updated = await _api.CreateClient(owner).UpdateRoleAsync(tenant.Id, roleId, "BILLING", ["tenant.members.read"]);

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await NameOfAsync(roleId)).ShouldBe("BILLING");
    }

    [Theory]
    [InlineData("tenant.nothing.read", HttpStatusCode.BadRequest, "role.permission_invalid")]
    [InlineData("tenant.owners.manage", HttpStatusCode.UnprocessableEntity, "role.permission_owner_only")]
    public async Task UpdateRole_APermissionACustomRoleMayNotHold_IsRefused(string permission, HttpStatusCode status, string code)
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");

        var updated = await _api.CreateClient(owner).UpdateRoleAsync(tenant.Id, roleId, "Billing", ["tenant.members.read", permission]);

        updated.StatusCode.ShouldBe(status);
        (await CodeOfAsync(updated)).ShouldBe(code);
        (await PermissionsOfAsync(roleId)).ShouldBe("tenant.members.read");
    }

    [Fact]
    public async Task UpdateRole_NoPermission_IsABadRequest()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");

        var updated = await _api.CreateClient(owner).UpdateRoleAsync(tenant.Id, roleId, "Billing", []);

        updated.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOfAsync(updated)).ShouldBe("role.permissions_required");
    }

    // Decision 2 of step r4b, before: an actor never changes a role that holds a permission their own role does not hold.
    [Fact]
    public async Task UpdateRole_ARoleStrongerThanTheActorsBefore_IsForbiddenAsAnEscalation()
    {
        var tenant = await _testData.AddTenantAsync();
        var keeper = await _testData.AddCustomRoleAsync(tenant.Id, "Role keeper", "tenant.roles.update", "tenant.members.read");
        var actor = await _testData.AddMemberWithRoleAsync(tenant.Id, keeper);
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Auditor", "tenant.members.read", "tenant.audit.read");

        var updated = await _api.CreateClient(actor).UpdateRoleAsync(tenant.Id, roleId, "Auditor", ["tenant.members.read"]);

        updated.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(updated)).ShouldBe("role.escalation");
        (await PermissionsOfAsync(roleId)).ShouldBe("tenant.audit.read, tenant.members.read");
    }

    // Decision 2 of step r4b, after: an actor never gives a role a permission their own role does not hold.
    [Fact]
    public async Task UpdateRole_APermissionTheActorsRoleDoesNotHold_IsForbiddenAsAnEscalation()
    {
        var tenant = await _testData.AddTenantAsync();
        var keeper = await _testData.AddCustomRoleAsync(tenant.Id, "Role keeper", "tenant.roles.update", "tenant.members.read");
        var actor = await _testData.AddMemberWithRoleAsync(tenant.Id, keeper);
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Reader", "tenant.members.read");

        var updated = await _api.CreateClient(actor).UpdateRoleAsync(tenant.Id, roleId, "Reader", ["tenant.members.read", "tenant.audit.read"]);

        updated.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(updated)).ShouldBe("role.escalation");
    }

    [Fact]
    public async Task UpdateRole_ByACustomRoleThatHoldsExactlyThePermissionsBeforeAndAfter_ChangesIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var keeper = await _testData.AddCustomRoleAsync(tenant.Id, "Role keeper", "tenant.roles.update", "tenant.members.read", "tenant.profile.read");
        var actor = await _testData.AddMemberWithRoleAsync(tenant.Id, keeper);
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Reader", "tenant.members.read");

        var updated = await _api.CreateClient(actor).UpdateRoleAsync(tenant.Id, roleId, "Reader", ["tenant.members.read", "tenant.profile.read"]);

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private Task<string?> NameOfAsync(Guid roleId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT name FROM control_plane.roles WHERE id = '{roleId}'");

    private Task<string?> PermissionsOfAsync(Guid roleId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg(permission, ', ' ORDER BY permission) FROM control_plane.role_permissions WHERE role_id = '{roleId}'");
}
