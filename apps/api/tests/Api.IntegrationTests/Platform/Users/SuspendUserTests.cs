using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform.Users;

// POST /v1/platform/users/{userId}/suspend: a system admin suspends a user through the system door. The identity provider is
// not told. A system admin cannot be suspended, so the system door always stays open to system admins. SuspendedUserTests follows what
// a suspended user meets.
public sealed class SuspendUserTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    // The rule of SuspendTenant's reason (the maintainer's decision of 2026-11-08).
    public static TheoryData<string, string> WrongReasons => new()
    {
        { """{}""", "required" },
        { """{"reason":null}""", "required" },
        { """{"reason":""}""", "required" },
        { """{"reason":"   "}""", "required" },
        { $$"""{"reason":"{{new string('r', 501)}}"}""", "too_long" },
    };

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task SuspendUser_ActiveUser_AnswersTheUserSuspended()
    {
        var user = await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", name: "Ada Lovelace");
        var id = await _testData.UserIdOfAsync(user);

        var response = await (await SystemAdminAsync()).SuspendUserAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        body.EnumerateObject().Select(field => field.Name)
            .ShouldBe(["id", "email", "name", "status", "isSystemAdmin", "isRootSystemAdmin", "systemAdminGrantedAt", "membershipCount", "createdAt"], ignoreOrder: true);
        body.GetProperty("id").GetGuid().ShouldBe(id);
        body.GetProperty("name").GetString().ShouldBe("Ada Lovelace");
        body.GetProperty("status").GetString().ShouldBe("suspended");
        body.GetProperty("isSystemAdmin").GetBoolean().ShouldBeFalse();
        (await SuspendedAtAsync(user)).ShouldNotBeNull();
    }

    // Suspending sets a state: a repeat answers the same and keeps the time of the first suspension.
    [Fact]
    public async Task SuspendUser_Twice_KeepsTheFirstTime()
    {
        var user = await _testData.AddUserAsync();
        var id = await _testData.UserIdOfAsync(user);
        var admin = await SystemAdminAsync();
        await admin.SuspendUserAsync(id);
        var first = await SuspendedAtAsync(user);

        var again = await admin.SuspendUserAsync(id);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("status").GetString().ShouldBe("suspended");
        (await SuspendedAtAsync(user)).ShouldBe(first);
    }

    [Fact]
    public async Task SuspendUser_ASystemAdmin_IsAConflictAndChangesNothing()
    {
        var systemAdmin = await _testData.AddSystemAdminAsync();

        var response = await (await SystemAdminAsync()).SuspendUserAsync(await _testData.UserIdOfAsync(systemAdmin));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("user.is_system_admin");
        (await SuspendedAtAsync(systemAdmin)).ShouldBeNull();
    }

    // One error code has one text (N11w): suspension answers the text that deletion answers.
    [Fact]
    public async Task SuspendUser_ASystemAdmin_AnswersTheTextOfTheCode()
    {
        var systemAdmin = await _testData.AddSystemAdminAsync();

        var response = await (await SystemAdminAsync()).SuspendUserAsync(await _testData.UserIdOfAsync(systemAdmin));

        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Detail.ShouldBe("A system admin cannot be suspended or deleted.");
    }

    [Fact]
    public async Task SuspendUser_Oneself_IsAConflict()
    {
        var admin = await _testData.AddSystemAdminAsync();

        var response = await _api.CreateClient(admin, secondFactor: true)
            .SuspendUserAsync(await _testData.UserIdOfAsync(admin));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("user.is_system_admin");
    }

    // The user's page shows the reason, without the spaces around it, to the platform only.
    [Fact]
    public async Task SuspendUser_WithAReason_KeepsItTrimmedAndGetUserShowsIt()
    {
        var id = await _testData.UserIdOfAsync(await _testData.AddUserAsync());
        var admin = await SystemAdminAsync();

        (await admin.SuspendUserAsync(id, "  Shared account ")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation)).GetProperty("suspensionReason").GetString().ShouldBe("Shared account");
    }

    [Fact]
    public async Task SuspendUser_AReasonOfFiveHundredCharactersWithSpacesAround_IsAccepted()
    {
        var id = await _testData.UserIdOfAsync(await _testData.AddUserAsync());
        var reason = new string('r', 500);
        var admin = await SystemAdminAsync();

        (await admin.SuspendUserAsync(id, $" {reason} ")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation)).GetProperty("suspensionReason").GetString().ShouldBe(reason);
    }

    [Theory]
    [MemberData(nameof(WrongReasons))]
    public async Task SuspendUser_WithoutAReasonOrWithOneTooLong_IsABadRequestAndChangesNothing(string body, string fieldCode)
    {
        var user = await _testData.AddUserAsync();

        var response = await (await SystemAdminAsync()).PostAsync(
            $"/v1/platform/users/{await _testData.UserIdOfAsync(user)}/suspend", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetProperty("reason").EnumerateArray().Select(code => code.GetString()).ShouldBe([fieldCode]);
        (await SuspendedAtAsync(user)).ShouldBeNull();
    }

    // A second suspension keeps the first reason, as it keeps the first time.
    [Fact]
    public async Task SuspendUser_Twice_KeepsTheFirstReason()
    {
        var id = await _testData.UserIdOfAsync(await _testData.AddUserAsync());
        var admin = await SystemAdminAsync();
        await admin.SuspendUserAsync(id, "Shared account");

        await admin.SuspendUserAsync(id, "Another reason");

        (await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation)).GetProperty("suspensionReason").GetString().ShouldBe("Shared account");
    }

    // The reason is personal data about the user, and the platform's log stays after the user's deletion (A8), so the user.suspended entry
    // holds no reason (the maintainer's decision of 2026-11-08).
    [Fact]
    public async Task SuspendUser_WithAReason_RecordsNoReasonInThePlatformEntry()
    {
        var id = await _testData.UserIdOfAsync(await _testData.AddUserAsync());

        await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).SuspendUserAsync(id, "Shared account"));

        (await _audit.PlatformDetailsAsync(id)).ShouldBe("-");
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    private Task<DateTime?> SuspendedAtAsync(string externalId) =>
        database.ScalarAsSuperuserAsync<DateTime?>($"SELECT suspended_at FROM control_plane.users WHERE external_id = '{externalId}'");
}
