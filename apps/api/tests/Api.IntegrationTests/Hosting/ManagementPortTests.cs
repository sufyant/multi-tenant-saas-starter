using System.Globalization;
using System.Net;
using Tenancy;

namespace Api.IntegrationTests.Hosting;

// Section 8: a process answers the health endpoints on its management port only, and the API on its public port only. The port is
// the one the connection arrived on, never the one the Host header names. Each host runs on Kestrel, on ports and a database of its
// own, so no other test's host takes part.
public sealed class ManagementPortTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task CheckHealth_OnTheManagementPortOfAWebHost_IsHealthy(string path)
    {
        await using var web = await HostAsync("web");

        var response = await web.ManagementClient().GetAsync(path, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task CheckHealth_OnThePublicPortOfAWebHost_IsNotFound(string path)
    {
        await using var web = await HostAsync("web");

        var response = await web.PublicClient().GetAsync(path, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CallTheApi_OnThePublicPortOfAWebHostWithoutAToken_IsUnauthorized()
    {
        await using var web = await HostAsync("web");

        var response = await web.PublicClient().GetAsync("/v1/me/tenants", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // Not found rather than unauthorized: nothing on the management port asks who the caller is.
    [Fact]
    public async Task CallTheApi_OnTheManagementPortOfAWebHost_IsNotFound()
    {
        await using var web = await HostAsync("web");

        var response = await web.ManagementClient().GetAsync("/v1/me/tenants", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Microsoft Learn, Health checks in ASP.NET Core, Require host: a client can send any Host header.
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task CheckHealth_OnThePublicPortWithAHostHeaderNamingTheManagementPort_IsNotFound(string path)
    {
        await using var web = await HostAsync("web");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = $"localhost:{web.Ports.Management}";

        var response = await web.PublicClient().SendAsync(request, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Kestrel ignores ASPNETCORE_HTTP_PORTS once it is given an endpoint of its own; the management port must not drop the public port
    // the container image sets that way.
    [Fact]
    public async Task CallTheApi_PublicPortFromHttpPorts_IsServedOnThatPort()
    {
        await using var web = await HostAsync("web", ports => new() { ["urls"] = null, ["http_ports"] = ports.Public.ToString(CultureInfo.InvariantCulture) });

        var response = await web.PublicClient().GetAsync("/v1/me/tenants", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task CheckHealth_OnTheManagementPortOfAWorkerHost_IsHealthy(string path)
    {
        await using var worker = await HostAsync("worker");

        var response = await worker.ManagementClient().GetAsync(path, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // A worker has no business endpoints, so it opens no public port.
    [Fact]
    public async Task Connect_ToThePublicPortOfAWorkerHost_IsRefused()
    {
        await using var worker = await HostAsync("worker");

        var call = () => worker.PublicClient().GetAsync("/health/live", Cancellation);

        await call.ShouldThrowAsync<HttpRequestException>();
    }

    private async Task<ApiFactory> HostAsync(string role, Func<HostPorts, Dictionary<string, string?>>? settingsFor = null)
    {
        var ports = HostPorts.Free();
        var settings = settingsFor?.Invoke(ports) ?? [];
        settings["Host:Role"] = role;

        return new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application, await database.CreateMigratedDatabaseAsync()),
            settings: settings,
            ports: ports);
    }
}
