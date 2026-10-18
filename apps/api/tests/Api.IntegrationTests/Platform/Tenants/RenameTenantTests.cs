using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform.Tenants;

// PATCH /v1/platform/tenants/{tenantId}: only the platform renames a tenant, through the system door; the slug never changes (K4). A new
// name is announced, and Audit records it under the tenant with the old and the new name; the name the tenant has announces nothing.
public sealed class RenameTenantTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private readonly AuditEntries _audit = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [InlineData("Active", "active")]
    [InlineData("Suspended", "suspended")]
    public async Task RenameTenant_ActiveOrSuspended_AnswersTheTenantWithTheNameTrimmedAndTheSameSlug(string status, string answered)
    {
        var tenant = await _catalog.AddTenantAsync(status: status);

        var response = await (await AdminAsync()).RenameTenantAsync(tenant.Id, "  Globex Corp ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (body.GetProperty("id").GetGuid(), body.GetProperty("name").GetString(), body.GetProperty("slug").GetString(), body.GetProperty("status").GetString())
            .ShouldBe((tenant.Id, "Globex Corp", tenant.Slug, answered));
        (await StoredNameAsync(tenant.Id)).ShouldBe("Globex Corp");
    }

    // K3: the platform's log names the system admin; the tenant's own log says only that the platform acted. The target keeps the new
    // name, and the details hold both.
    [Fact]
    public async Task RenameTenant_ANewName_IsRecordedOnceInBothLogsWithTheOldAndTheNewName()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await _catalog.AddSystemAdminAsync();

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(admin, secondFactor: true).RenameTenantAsync(tenant.Id, "Globex Corp"));

        var adminId = await _catalog.UserIdOfAsync(admin);
        (await _audit.PlatformLogAsync(tenant.Id)).ShouldBe($"tenant.renamed|User|{adminId}|Tenant|{tenant.Id}|Globex Corp");
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe($"{tenant.Id}:tenant.renamed|Platform|-|Tenant|{tenant.Id}|Globex Corp");
        (await _audit.PlatformDetailsAsync(tenant.Id)).ShouldBe("""{"newName": "Globex Corp", "oldName": "Acme Ltd"}""");
        (await _audit.TenantDetailsAsync(tenant.Id)).ShouldBe("""{"newName": "Globex Corp", "oldName": "Acme Ltd"}""");
    }

    // Renaming sets a state (I1): the name the tenant has answers as it is and writes nothing.
    [Theory]
    [InlineData("Acme Ltd")]
    [InlineData(" Acme Ltd  ")]
    public async Task RenameTenant_TheNameItHas_AnswersAsItIsAndRecordsNothing(string name)
    {
        var tenant = await _catalog.AddTenantAsync();

        var response = await _api.WaitingForMessagesAsync(async () => await (await AdminAsync()).RenameTenantAsync(tenant.Id, name));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("name").GetString().ShouldBe("Acme Ltd");
        (await _audit.PlatformLogAsync(tenant.Id)).ShouldBeNull();
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBeNull();
    }

    public static TheoryData<string> InvalidNames => ["", "   "];

    // The tenant checks the name as at its creation.
    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task RenameTenant_InvalidName_IsABadRequestAndChangesNothing(string name)
    {
        var tenant = await _catalog.AddTenantAsync();

        var response = await (await AdminAsync()).RenameTenantAsync(tenant.Id, name);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOfAsync(response)).ShouldBe("tenant.name_invalid");
        (await StoredNameAsync(tenant.Id)).ShouldBe("Acme Ltd");
    }

    // The maintainer's decision 2 after audit fix 09: the validator checks the name's trimmed length, so the document states it.
    [Fact]
    public async Task RenameTenant_NameTooLong_IsAValidationProblemAndChangesNothing()
    {
        var tenant = await _catalog.AddTenantAsync();

        var response = await (await AdminAsync()).RenameTenantAsync(tenant.Id, $" {new string('n', 101)} ");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetProperty("name").EnumerateArray().Select(code => code.GetString()).ShouldBe(["too_long"]);
        (await StoredNameAsync(tenant.Id)).ShouldBe("Acme Ltd");
    }

    [Fact]
    public async Task RenameTenant_WithoutAName_IsABadRequest()
    {
        var tenant = await _catalog.AddTenantAsync();

        var response = await (await AdminAsync()).PatchAsJsonAsync($"/v1/platform/tenants/{tenant.Id}", new { }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetProperty("name").EnumerateArray().Select(code => code.GetString()).ShouldBe(["required"]);
    }

    [Theory]
    [InlineData("Provisioning")]
    [InlineData("Cancelled")]
    [InlineData("Closed")]
    public async Task RenameTenant_NeitherActiveNorSuspended_IsAConflictAndChangesNothing(string status)
    {
        var tenant = await _catalog.AddTenantAsync(status: status, closedAt: DateTimeOffset.UtcNow);

        var response = await (await AdminAsync()).RenameTenantAsync(tenant.Id, "Globex Corp");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_active");
        (await StoredNameAsync(tenant.Id)).ShouldBe("Acme Ltd");
    }

    [Fact]
    public async Task RenameTenant_UnknownTenant_IsNotFound()
    {
        var response = await (await AdminAsync()).RenameTenantAsync(Guid.NewGuid(), "Globex Corp");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_found");
    }

    // The maintainer's decision: the tenant's name is locked in the tenant app, so the tenant path has no PATCH, not even for an
    // Owner.
    [Fact]
    public async Task RenameTenant_ThroughTheTenantDoor_IsNoEndpoint()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));

        var response = await owner.PatchAsJsonAsync($"/v1/tenants/{tenant.Id}", new { name = "Globex Corp" }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await StoredNameAsync(tenant.Id)).ShouldBe("Acme Ltd");
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private Task<string?> StoredNameAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT name FROM control_plane.tenants WHERE id = '{tenantId}'");

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
