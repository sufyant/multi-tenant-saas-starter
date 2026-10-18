using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;

namespace Api.IntegrationTests.Observability;

[RuntimeHandlerCode]
public sealed class RequestLoggingTests(Database database) : IAsyncLifetime
{
    private const string RequestLogCategory = "Serilog.AspNetCore.RequestLoggingMiddleware";

    private PipelineHost _host = null!;

    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await PipelineHost.StartAsync(configureServices: services => services.AddFakeLogging());
        _client = _host.CreateClient();
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task LogRequest_ApiRequest_IsLogged()
    {
        await _client.GetAsync("/v1/ping", TestContext.Current.CancellationToken);

        RequestLogs().ShouldHaveSingleItem().Message.ShouldContain("/v1/ping");
    }

    [Fact]
    public async Task LogRequest_TenantRequest_IsLoggedWithItsTenant()
    {
        var catalog = new Catalog(database);
        var tenant = await catalog.AddTenantAsync();
        var member = await catalog.AddMemberAsync(tenant.Id);

        await _host.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/ping", TestContext.Current.CancellationToken);

        RequestLogs().ShouldHaveSingleItem().StructuredState.ShouldNotBeNull().ShouldContain(new KeyValuePair<string, string?>("TenantId", tenant.Id.ToString()));
    }

    [Fact]
    public async Task LogRequest_WithAQueryString_LeavesTheQueryStringOut()
    {
        await _client.GetAsync("/v1/ping?access_token=secret-token", TestContext.Current.CancellationToken);

        RequestLogs().ShouldHaveSingleItem().Message.ShouldNotContain("secret-token");
    }

    // The health checks answer only on the management port, which the test server does not have; the host runs on Kestrel here.
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task LogRequest_HealthCheck_IsNotLogged(string path)
    {
        await using var api = new ApiFactory(
            database.ApplicationConnectionString, configureServices: services => services.AddFakeLogging(), ports: HostPorts.Free());

        var response = await api.ManagementClient().GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        RequestLogsOf(api.Services).ShouldBeEmpty();
    }

    private static List<FakeLogRecord> RequestLogsOf(IServiceProvider services) =>
        [.. services.GetFakeLogCollector().GetSnapshot().Where(record => record.Category == RequestLogCategory)];

    private List<FakeLogRecord> RequestLogs() => RequestLogsOf(_host.Services);
}
