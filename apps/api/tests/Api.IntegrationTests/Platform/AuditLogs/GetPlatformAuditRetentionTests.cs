using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.AuditLogs;

// GET /v1/platform/audit-log/retention: how long the platform's log keeps an entry, the deployment setting Audit:Retention:Days (K15).
public sealed class GetPlatformAuditRetentionTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: new Dictionary<string, string?> { ["Audit:Retention:Days"] = "100" },
            configureServices: services =>
            {
                foreach (var job in services.Where(service => service.ImplementationType?.Name == "AuditRetentionJob").ToList())
                {
                    services.Remove(job);
                }
            });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task GetPlatformAuditRetention_AsASystemAdmin_AnswersTheSetting()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var response = await admin.GetAsync("/v1/platform/audit-log/retention", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        answer.EnumerateObject().Select(field => $"{field.Name}={field.Value}").ShouldBe(["days=100"]);
    }

    [Fact]
    public async Task GetPlatformAuditRetention_AsATenantOwner_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var response = await _api.CreateClient(owner, secondFactor: true).GetAsync("/v1/platform/audit-log/retention", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
