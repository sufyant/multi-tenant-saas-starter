using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests;

// Section 8 (API8), on the host with the modules' endpoints: a browser script may read every response header that the OpenAPI document
// declares. The exposed headers come from the endpoints' metadata, so the test compares them with the document.
public sealed class CorsExposedHeadersTests(Database database) : IAsyncLifetime
{
    private const string AllowedOrigin = "https://app.test";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: new Dictionary<string, string?> { ["Host:Cors:AllowedOrigins:0"] = AllowedOrigin });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // CreateTenant sends Location on its 201 and Idempotent-Replayed on a replay (I5, N11).
    [Fact]
    public async Task CreateTenant_FromAnAllowedOrigin_ExposesLocationAndIdempotentReplayed()
    {
        var admin = await CrossOriginAdminAsync();

        var created = await _api.WaitingForMessagesAsync(() => admin.CreateTenantAsync(
            new { name = "Acme Ltd", slug = $"tenant-{Guid.NewGuid():N}"[..20], ownerEmail = $"{Guid.NewGuid():N}@example.com" }));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([AllowedOrigin]);
        ExposedHeadersOf(created).ShouldContain("location");
        ExposedHeadersOf(created).ShouldContain("idempotent-replayed");
    }

    [Fact]
    public async Task Request_FromAnAllowedOrigin_ExposesTheResponseHeadersTheDocumentDeclares()
    {
        var admin = await CrossOriginAdminAsync();
        var document = await _api.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json", Cancellation);

        var response = await admin.GetAsync("/v1/me", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        DeclaredResponseHeaders.Of(document).ShouldNotBeEmpty();
        ExposedHeadersOf(response).ShouldBe(DeclaredResponseHeaders.Of(document), ignoreOrder: true);
    }

    private async Task<HttpClient> CrossOriginAdminAsync()
    {
        var admin = _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);
        admin.DefaultRequestHeaders.Add("Origin", AllowedOrigin);
        return admin;
    }

    private static string[] ExposedHeadersOf(HttpResponseMessage response) =>
    [
        .. response.Headers.GetValues("Access-Control-Expose-Headers")
            .SelectMany(value => value.Split(','))
            .Select(header => header.Trim().ToLowerInvariant()),
    ];
}
