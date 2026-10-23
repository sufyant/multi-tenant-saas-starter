using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Roles;

// DELETE /v1/tenants/{tenantId}/roles/{roleId}: a member whose role holds tenant.roles.delete deletes a custom role that no membership and
// no pending invitation uses (A3). A finished invitation keeps no role then (ON DELETE SET NULL), and its audit entries keep the role's
// name (K3).
public sealed class DeleteRoleTests(Database database) : IAsyncLifetime
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
    public async Task DeleteRole_ARoleNobodyUses_DeletesItWithItsPermissionsAndRecordsIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");

        var deleted = await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).DeleteRoleAsync(tenant.Id, roleId));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountAsync($"SELECT count(*) FROM control_plane.roles WHERE id = '{roleId}'")).ShouldBe(0);
        (await CountAsync($"SELECT count(*) FROM control_plane.role_permissions WHERE role_id = '{roleId}'")).ShouldBe(0);
        (await _audit.TenantLogAsync(roleId)).ShouldBe($"{tenant.Id}:role.deleted|User|{await _testData.UserIdOfAsync(owner)}|Role|{roleId}|Billing");
    }

    [Fact]
    public async Task DeleteRole_ARoleAMemberHas_IsAConflictAndKeepsIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");
        await _testData.AddMemberWithRoleAsync(tenant.Id, roleId);

        var deleted = await _api.CreateClient(owner).DeleteRoleAsync(tenant.Id, roleId);

        deleted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(deleted)).ShouldBe("role.in_use");
        (await CountAsync($"SELECT count(*) FROM control_plane.roles WHERE id = '{roleId}'")).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteRole_ARoleAPendingInvitationGives_IsAConflictAndKeepsIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");
        await _testData.AddInvitationAsync(tenant.Id, roleId: roleId);

        var deleted = await _api.CreateClient(owner).DeleteRoleAsync(tenant.Id, roleId);

        deleted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(deleted)).ShouldBe("role.in_use");
    }

    // A finished invitation still names its role; the deletion takes it away and keeps the invitation and its tenant.
    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Accepted")]
    public async Task DeleteRole_ARoleOnlyAFinishedInvitationGave_DeletesItAndTheInvitationKeepsNoRole(string status)
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");
        var invitationId = await _testData.AddInvitationAsync(tenant.Id, roleId: roleId, status: status);

        var deleted = await _api.CreateClient(owner).DeleteRoleAsync(tenant.Id, roleId);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT concat_ws('|', tenant_id, coalesce(role_id::text, '-'), status) FROM control_plane.invitations WHERE id = '{invitationId}'"))
            .ShouldBe($"{tenant.Id}|-|{status}");
    }

    // A2: the built-in roles cannot be deleted.
    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("Member")]
    public async Task DeleteRole_ABuiltInRole_IsAConflict(string builtIn)
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var deleted = await _api.CreateClient(owner).DeleteRoleAsync(tenant.Id, await _testData.RoleIdOfAsync(tenant.Id, builtIn));

        deleted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(deleted)).ShouldBe("role.built_in");
    }

    [Fact]
    public async Task DeleteRole_ARoleOfAnotherTenant_IsNotFoundAndKeepsIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var other = await _testData.AddTenantAsync();
        var roleId = await _testData.AddCustomRoleAsync(other.Id, "Billing", "tenant.members.read");

        var deleted = await _api.CreateClient(owner).DeleteRoleAsync(tenant.Id, roleId);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(deleted)).ShouldBe("role.not_found");
        (await CountAsync($"SELECT count(*) FROM control_plane.roles WHERE id = '{roleId}'")).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteRole_ByAnAdmin_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await _testData.AddMemberAsync(tenant.Id, role: "Admin");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");

        var deleted = await _api.CreateClient(admin).DeleteRoleAsync(tenant.Id, roleId);

        deleted.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Decision 2 of step r4b: nobody deletes a role stronger than their own.
    [Fact]
    public async Task DeleteRole_ARoleStrongerThanTheActors_IsForbiddenAsAnEscalation()
    {
        var tenant = await _testData.AddTenantAsync();
        var keeper = await _testData.AddCustomRoleAsync(tenant.Id, "Role keeper", "tenant.roles.delete", "tenant.members.read");
        var actor = await _testData.AddMemberWithRoleAsync(tenant.Id, keeper);
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Auditor", "tenant.members.read", "tenant.audit.read");

        var deleted = await _api.CreateClient(actor).DeleteRoleAsync(tenant.Id, roleId);

        deleted.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(deleted)).ShouldBe("role.escalation");
    }

    // A pending invitation always has a role: the CHECK refuses one without.
    [Fact]
    public async Task StoreInvitation_PendingWithoutARole_IsRefused()
    {
        var tenant = await _testData.AddTenantAsync();
        var invitationId = await _testData.AddInvitationAsync(tenant.Id);

        var clear = () => database.ExecuteInTenantAsync(tenant.Id, $"UPDATE control_plane.invitations SET role_id = NULL WHERE id = '{invitationId}'");

        (await clear.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    // A membership keeps RESTRICT: the database refuses to delete a role a member has, also behind the tenant's rule.
    [Fact]
    public async Task StoreRole_DeleteARoleAMemberHas_IsRefusedByTheDatabase()
    {
        var tenant = await _testData.AddTenantAsync();
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");
        await _testData.AddMemberWithRoleAsync(tenant.Id, roleId);

        var delete = () => database.ExecuteInTenantAsync(tenant.Id, $"DELETE FROM control_plane.roles WHERE id = '{roleId}'");

        (await delete.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.RestrictViolation);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private Task<long> CountAsync(string sql) => database.ScalarAsSuperuserAsync<long>(sql);
}
