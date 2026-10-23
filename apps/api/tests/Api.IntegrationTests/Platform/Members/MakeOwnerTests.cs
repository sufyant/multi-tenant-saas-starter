using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Tenancy;

namespace Api.IntegrationTests.Platform.Members;

// POST /v1/platform/tenants/{tenantId}/owners: Owner recovery (section 6). The platform makes a member of a tenant an Owner, whether or not the
// tenant has one: the only Owner may have left the company and not answer. The old Owner stays, and the new Owner can remove them. The
// request names the user by id, from the user's detail; the platform reads no member list of the tenant.
public sealed class MakeOwnerTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private readonly TenantSummaries _summaries = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // The answer is the user's membership, as the user's detail shows it, so the platform's screen shows the new role at once.
    [Fact]
    public async Task MakeOwner_AMemberOfATenantWithoutAnOwner_MakesThemTheOwnerAndCountsThem()
    {
        var tenant = await _testData.AddTenantAsync(name: "Alpha Ltd");
        var joined = new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);
        var member = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id, role: "Member", joinedAt: joined));
        await _testData.SummarizeAsync(tenant.Id);

        var made = await (await SystemAdminAsync()).MakeOwnerAsync(tenant.Id, member);

        made.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await made.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        body.EnumerateObject().Select(field => field.Name).ShouldBe(
            ["tenantId", "tenantName", "tenantSlug", "tenantStatus", "role", "joinedAt", "lastOwner"], ignoreOrder: true);
        (body.GetProperty("tenantId").GetGuid(), body.GetProperty("tenantName").GetString(), body.GetProperty("tenantSlug").GetString())
            .ShouldBe((tenant.Id, "Alpha Ltd", tenant.Slug));
        (body.GetProperty("tenantStatus").GetString(), body.GetProperty("joinedAt").GetDateTimeOffset(), body.GetProperty("lastOwner").GetBoolean())
            .ShouldBe(("active", joined, true));
        RoleAnswers.Of(body).ShouldBe(RoleAnswers.BuiltIn("owner"));
        (await RoleOfAsync(tenant.Id, member)).ShouldBe("Owner");
        (await _summaries.StoredAsync(tenant.Id)).ShouldStartWith("1|1|");
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // The tenant has an Owner on paper, but nobody can manage it. The old Owner keeps the role.
    [Fact]
    public async Task MakeOwner_AMemberOfATenantWithAnOwner_AddsAnOwnerAndTheOldOneStays()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var admin = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id, role: "Admin"));
        await _testData.SummarizeAsync(tenant.Id);

        var made = await (await SystemAdminAsync()).MakeOwnerAsync(tenant.Id, admin);

        made.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await made.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("lastOwner").GetBoolean().ShouldBeFalse();
        ((await RoleOfAsync(tenant.Id, owner)), (await RoleOfAsync(tenant.Id, admin))).ShouldBe(("Owner", "Owner"));
        (await _summaries.StoredAsync(tenant.Id)).ShouldStartWith("2|2|");
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // Recovery is complete only when the new Owner can manage the tenant: they remove the old Owner, under the rules of K1.
    [Fact]
    public async Task MakeOwner_TheNewOwner_RemovesTheOldOwner()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await _testData.AddMemberAsync(tenant.Id, role: "Member");
        await _testData.SummarizeAsync(tenant.Id);
        await (await SystemAdminAsync()).MakeOwnerAsync(tenant.Id, await _testData.UserIdOfAsync(member));

        var removed = await _api.CreateClient(member).RemoveMemberAsync(tenant.Id, owner);

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RoleOfAsync(tenant.Id, owner)).ShouldBeNull();
        (await _summaries.StoredAsync(tenant.Id)).ShouldStartWith("1|1|");
    }

    // K3: the tenant's log names the platform, and the platform's log the system admin. Both hold the old and the new role, as a
    // member's role change does, and the member is the target, by id.
    [Fact]
    public async Task MakeOwner_AMember_RecordsTheRoleChangeInBothLogs()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id, role: "Member"));
        var adminId = await _testData.AddSystemAdminAsync();
        var admin = await _testData.UserIdOfAsync(adminId);

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(adminId, secondFactor: true).MakeOwnerAsync(tenant.Id, member));

        (await _audit.TenantLogAsync(member)).ShouldBe($"{tenant.Id}:member.role_changed|Platform|-|User|{member}|-");
        (await _audit.PlatformLogAsync(member)).ShouldBe($"member.role_changed|User|{admin}|User|{member}|-");
        var details =
            $$"""{"newRole": "owner", "oldRole": "member", "newRoleId": "{{await _testData.RoleIdOfAsync(tenant.Id, "Owner")}}", "oldRoleId": "{{await _testData.RoleIdOfAsync(tenant.Id, "Member")}}", "newRoleBuiltIn": true, "oldRoleBuiltIn": true}""";
        ((await _audit.TenantDetailsAsync(member)), (await _audit.PlatformDetailsAsync(member))).ShouldBe((details, details));
    }

    [Fact]
    public async Task MakeOwner_AnIdNoTenantHas_IsNotFound()
    {
        var made = await (await SystemAdminAsync()).MakeOwnerAsync(Guid.NewGuid(), Guid.NewGuid());

        made.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(made)).ShouldBe("tenant.not_found");
    }

    // The rule of InviteOwner: only an active tenant gets an Owner from the platform.
    [Theory]
    [InlineData("Suspended")]
    [InlineData("Closed")]
    public async Task MakeOwner_ATenantThatIsNotActive_IsAConflictAndChangesNothing(string status)
    {
        var tenant = await _testData.AddTenantAsync(status: status);
        var member = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id, role: "Member"));

        var made = await (await SystemAdminAsync()).MakeOwnerAsync(tenant.Id, member);

        made.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(made)).ShouldBe("tenant.not_active");
        (await RoleOfAsync(tenant.Id, member)).ShouldBe("Member");
    }

    // Row level security hides another tenant's members: a member of another tenant is no member here.
    [Fact]
    public async Task MakeOwner_AUserOfAnotherTenant_IsNotFoundAndChangesNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var other = await _testData.AddTenantAsync();
        var stranger = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(other.Id, role: "Member"));

        var made = await (await SystemAdminAsync()).MakeOwnerAsync(tenant.Id, stranger);

        made.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(made)).ShouldBe("member.not_found");
        (await RoleOfAsync(other.Id, stranger)).ShouldBe("Member");
        (await RoleOfAsync(tenant.Id, stranger)).ShouldBeNull();
    }

    [Fact]
    public async Task MakeOwner_AnIdNoUserHas_IsNotFound()
    {
        var tenant = await _testData.AddTenantAsync();

        var made = await (await SystemAdminAsync()).MakeOwnerAsync(tenant.Id, Guid.NewGuid());

        made.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(made)).ShouldBe("member.not_found");
    }

    // The action makes an Owner; an Owner already is one. A repeated request answers the same.
    [Fact]
    public async Task MakeOwner_AnOwner_IsAConflictAndRecordsNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));

        var made = await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).MakeOwnerAsync(tenant.Id, owner));

        made.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(made)).ShouldBe("member.already_owner");
        (await _audit.TenantLogAsync(owner)).ShouldBeNull();
    }

    // A suspended user is refused at the tenant door (A9), so they could not manage the tenant.
    [Fact]
    public async Task MakeOwner_ASuspendedUser_IsAConflictAndChangesNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var externalId = await _testData.AddMemberAsync(tenant.Id, role: "Member");
        await _testData.SuspendUserAsync(externalId);
        var member = await _testData.UserIdOfAsync(externalId);

        var made = await (await SystemAdminAsync()).MakeOwnerAsync(tenant.Id, member);

        made.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(made)).ShouldBe("user.suspended");
        (await RoleOfAsync(tenant.Id, member)).ShouldBe("Member");
    }

    // A deleted user has no membership (section 6, "User deletion"); the row is written by hand here, to check the rule itself.
    [Fact]
    public async Task MakeOwner_ADeletedUser_IsAConflictAndChangesNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var externalId = await _testData.AddMemberAsync(tenant.Id, role: "Member");
        await _testData.DeleteUserAsync(externalId);
        var member = await _testData.UserIdOfAsync(externalId);

        var made = await (await SystemAdminAsync()).MakeOwnerAsync(tenant.Id, member);

        made.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(made)).ShouldBe("user.deleted");
        (await RoleOfAsync(tenant.Id, member)).ShouldBe("Member");
    }

    // The user's id is required: a request without one never falls back to an empty id (K5).
    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "userId": "not-an-id" }""")]
    public async Task MakeOwner_WithoutAUserId_IsABadRequest(string body)
    {
        var tenant = await _testData.AddTenantAsync();

        var made = await (await SystemAdminAsync()).PostAsync(
            $"/v1/platform/tenants/{tenant.Id}/owners", new StringContent(body, Encoding.UTF8, "application/json"), Cancellation);

        made.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOfAsync(made)).ShouldBe("request.invalid");
    }

    // I1: the action changes a role and creates no resource, so it takes no idempotency key.
    [Fact]
    public void MapMakeOwner_TheEndpoint_RequiresItsPermissionAndNoIdempotencyKey()
    {
        var endpoint = _api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Single(found =>
            found.RoutePattern.RawText == "/v1/platform/tenants/{tenantId:guid}/owners"
            && found.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("POST"));

        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).ShouldContain("platform.tenants.make_owner");
        endpoint.Metadata.GetMetadata<IdempotencyKeyRequired>().ShouldBeNull();
    }

    [Fact]
    public async Task MakeOwner_AsATenantOwner_IsForbiddenAndChangesNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"), secondFactor: true);
        var member = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id, role: "Member"));

        var made = await owner.MakeOwnerAsync(tenant.Id, member);

        made.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await RoleOfAsync(tenant.Id, member)).ShouldBe("Member");
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    // The built-in kind of the member's role in the tenant, or null when the user is no member there.
    private Task<string?> RoleOfAsync(Guid tenantId, Guid userId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT roles.built_in FROM control_plane.memberships JOIN control_plane.roles ON roles.id = memberships.role_id
            WHERE memberships.tenant_id = '{tenantId}' AND memberships.user_id = '{userId}'
            """);
}
