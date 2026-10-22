using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Roles;

// POST /v1/tenants/{tenantId}/roles: a member whose role holds tenant.roles.create builds a custom role from the tenant pool (A3). The
// tenant's rules check, in this order, the actor's permission, the name, the permissions, the escalation rule and the limit (decisions
// 1 to 3 of step r4b). The limit is 2 here, so a test reaches it with two roles.
public sealed class CreateRoleTests(Database database) : IAsyncLifetime
{
    private const int Limit = 2;

    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: new Dictionary<string, string?> { ["ControlPlane:Roles:MaxCustomRolesPerTenant"] = $"{Limit}" });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task CreateRole_ByAnOwner_CreatesTheRoleAndRecordsIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var created = await _api.WaitingForMessagesAsync(() =>
            _api.CreateClient(owner).CreateRoleAsync(tenant.Id, "  Billing  ", ["tenant.members.read", "tenant.audit.read", "tenant.members.read"]));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.Location.ShouldBeNull();
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var roleId = body.GetProperty("id").GetGuid();
        (body.GetProperty("name").GetString(), body.GetProperty("builtIn").GetBoolean()).ShouldBe(("Billing", false));
        body.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()).ShouldBe(["tenant.audit.read", "tenant.members.read"]);
        (await PermissionsOfAsync(roleId)).ShouldBe("tenant.audit.read, tenant.members.read");
        (await _audit.TenantLogAsync(roleId)).ShouldBe($"{tenant.Id}:role.created|User|{await _testData.UserIdOfAsync(owner)}|Role|{roleId}|Billing");
        (await _audit.TenantDetailsAsync(roleId)).ShouldBe("""{"permissions": ["tenant.audit.read", "tenant.members.read"]}""");
    }

    [Fact]
    public async Task CreateRole_SameKeyTwice_CreatesOneRoleAndReplaysTheAnswer()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var key = Guid.NewGuid().ToString();

        var first = await owner.CreateRoleAsync(tenant.Id, "Billing", ["tenant.members.read"], key);
        var second = await owner.CreateRoleAsync(tenant.Id, "Billing", ["tenant.members.read"], key);

        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await second.Content.ReadAsStringAsync(Cancellation)).ShouldBe(await first.Content.ReadAsStringAsync(Cancellation));
        (await CustomRolesOfAsync(tenant.Id)).ShouldBe(1);
    }

    // Decision 1 of step r4b: of the built-in roles, only the Owner creates a role; the door refuses an Admin and a Member.
    [Theory]
    [InlineData("Admin")]
    [InlineData("Member")]
    public async Task CreateRole_ByAnAdminOrAMember_IsForbiddenAndCreatesNothing(string role)
    {
        var tenant = await _testData.AddTenantAsync();
        var actor = await _testData.AddMemberAsync(tenant.Id, role: role);

        var created = await _api.CreateClient(actor).CreateRoleAsync(tenant.Id, "Billing", ["tenant.members.read"]);

        created.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CustomRolesOfAsync(tenant.Id)).ShouldBe(0);
    }

    // Decision 1 of step r4b: the permission is not owner-only, so a custom role that holds it creates a role too.
    [Fact]
    public async Task CreateRole_ByACustomRoleThatHoldsExactlyItsPermissions_CreatesIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var keeper = await _testData.AddCustomRoleAsync(tenant.Id, "Role keeper", "tenant.roles.create", "tenant.members.read", "tenant.profile.read");
        var actor = await _testData.AddMemberWithRoleAsync(tenant.Id, keeper);

        var created = await _api.CreateClient(actor).CreateRoleAsync(tenant.Id, "Reader", ["tenant.members.read", "tenant.profile.read"]);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // Decision 2 of step r4b: nobody creates a role stronger than their own (Kubernetes RBAC, privilege escalation prevention).
    [Fact]
    public async Task CreateRole_APermissionTheActorsRoleDoesNotHold_IsForbiddenAsAnEscalation()
    {
        var tenant = await _testData.AddTenantAsync();
        var keeper = await _testData.AddCustomRoleAsync(tenant.Id, "Role keeper", "tenant.roles.create", "tenant.members.read", "tenant.profile.read");
        var actor = await _testData.AddMemberWithRoleAsync(tenant.Id, keeper);

        var created = await _api.CreateClient(actor).CreateRoleAsync(tenant.Id, "Auditor", ["tenant.members.read", "tenant.audit.read"]);

        created.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(created)).ShouldBe("role.escalation");
        (await CustomRolesOfAsync(tenant.Id)).ShouldBe(1);
    }

    // A3: a role's name is 1 to Role.NameMaxLength characters, trimmed. A blank one is the tenant's refusal; a long one, the validator's.
    [Theory]
    [InlineData("   ", HttpStatusCode.BadRequest, "role.name_invalid")]
    [InlineData("", HttpStatusCode.BadRequest, "role.name_invalid")]
    public async Task CreateRole_ABlankName_IsInvalid(string name, HttpStatusCode status, string code)
    {
        var (tenantId, owner) = await OwnerAsync();

        var created = await owner.CreateRoleAsync(tenantId, name, ["tenant.members.read"]);

        created.StatusCode.ShouldBe(status);
        (await CodeOfAsync(created)).ShouldBe(code);
    }

    [Fact]
    public async Task CreateRole_ANameOverTheLimit_IsAValidationProblem()
    {
        var (tenantId, owner) = await OwnerAsync();

        var created = await owner.CreateRoleAsync(tenantId, new string('r', 101), ["tenant.members.read"]);

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOfAsync(created)).ShouldBe("request.validation_failed");
    }

    // A3: unique in the tenant without regard to case, the built-in names included, so no custom role is called "Owner".
    [Theory]
    [InlineData("OWNER")]
    [InlineData("Billing")]
    [InlineData(" billing ")]
    public async Task CreateRole_ANameTheTenantHasInAnyCase_IsAConflict(string name)
    {
        var (tenantId, owner) = await OwnerAsync();
        await _testData.AddCustomRoleAsync(tenantId, "BILLING", "tenant.members.read");

        var created = await owner.CreateRoleAsync(tenantId, name, ["tenant.members.read"]);

        created.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(created)).ShouldBe("role.name_taken");
    }

    // The name of another tenant's role is free.
    [Fact]
    public async Task CreateRole_ANameAnotherTenantHas_CreatesIt()
    {
        var (tenantId, owner) = await OwnerAsync();
        var other = await _testData.AddTenantAsync();
        await _testData.AddCustomRoleAsync(other.Id, "Billing", "tenant.members.read");

        var created = await owner.CreateRoleAsync(tenantId, "Billing", ["tenant.members.read"]);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("tenant.nothing.read", HttpStatusCode.BadRequest, "role.permission_invalid")]
    [InlineData("platform.users.read", HttpStatusCode.BadRequest, "role.permission_invalid")]
    [InlineData("tenant.owners.manage", HttpStatusCode.UnprocessableEntity, "role.permission_owner_only")]
    public async Task CreateRole_APermissionACustomRoleMayNotHold_IsRefused(string permission, HttpStatusCode status, string code)
    {
        var (tenantId, owner) = await OwnerAsync();

        var created = await owner.CreateRoleAsync(tenantId, "Billing", ["tenant.members.read", permission]);

        created.StatusCode.ShouldBe(status);
        (await CodeOfAsync(created)).ShouldBe(code);
        (await CustomRolesOfAsync(tenantId)).ShouldBe(0);
    }

    [Fact]
    public async Task CreateRole_NoPermission_IsABadRequest()
    {
        var (tenantId, owner) = await OwnerAsync();

        var created = await owner.CreateRoleAsync(tenantId, "Nobody", []);

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOfAsync(created)).ShouldBe("role.permissions_required");
    }

    // Decision 3 of step r4b: the limit counts custom roles only, and a role at the limit is refused (OWASP API4).
    [Fact]
    public async Task CreateRole_AtTheLimit_IsAConflictAndCreatesNothing()
    {
        var (tenantId, owner) = await OwnerAsync();
        (await owner.CreateRoleAsync(tenantId, "First", ["tenant.members.read"])).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await owner.CreateRoleAsync(tenantId, "Second", ["tenant.members.read"])).StatusCode.ShouldBe(HttpStatusCode.Created);

        var third = await owner.CreateRoleAsync(tenantId, "Third", ["tenant.members.read"]);

        third.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(third)).ShouldBe("role.limit_reached");
        (await CustomRolesOfAsync(tenantId)).ShouldBe(Limit);
    }

    // A3, K6b: the unique index on the tenant and lower(name) is the second guard behind the tenant's lock.
    [Fact]
    public async Task StoreRole_ANameThatDiffersOnlyInCase_IsRefusedByTheIndex()
    {
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");

        var second = () => _testData.AddCustomRoleAsync(tenant.Id, "bILLING", "tenant.members.read");

        (await second.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    // A2, A3: a built-in role's permissions come from code, so it has no rows of role_permissions; only custom roles have them.
    [Fact]
    public async Task CreateRole_ACustomRole_WritesRowsOfItsOwnAndNoneForABuiltInRole()
    {
        var (tenantId, owner) = await OwnerAsync();

        await owner.CreateRoleAsync(tenantId, "Billing", ["tenant.members.read"]);

        (await database.ScalarAsSuperuserAsync<long>(
            $"""
            SELECT count(*) FROM control_plane.role_permissions JOIN control_plane.roles ON roles.id = role_permissions.role_id
            WHERE role_permissions.tenant_id = '{tenantId}' AND roles.built_in IS NOT NULL
            """)).ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.role_permissions WHERE tenant_id = '{tenantId}'")).ShouldBe(1);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<(Guid TenantId, HttpClient Owner)> OwnerAsync()
    {
        var tenant = await _testData.AddTenantAsync();
        return (tenant.Id, _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner")));
    }

    private Task<long> CustomRolesOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.roles WHERE tenant_id = '{tenantId}' AND built_in IS NULL");

    private Task<string?> PermissionsOfAsync(Guid roleId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg(permission, ', ' ORDER BY permission) FROM control_plane.role_permissions WHERE role_id = '{roleId}'");
}
