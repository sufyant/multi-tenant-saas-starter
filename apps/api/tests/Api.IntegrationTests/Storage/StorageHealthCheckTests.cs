using System.Net;
using Tenancy;

namespace Api.IntegrationTests.Storage;

// Section 7, "File storage", rule 7: a process is ready only once it has written, read and deleted a file in its bucket.
public sealed class StorageHealthCheckTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CheckHealth_StorageAnswers_IsReadyAndLeavesNoFile()
    {
        await using var api = new ApiFactory(
            database.ApplicationConnectionString,
            settings: new Dictionary<string, string?> { ["Host:Storage:Bucket"] = ObjectStorage.HealthBucket },
            ports: HostPorts.Free());

        var ready = await api.ManagementClient().GetAsync("/health/ready", Cancellation);

        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ObjectStorage.KeysAsync("health/", ObjectStorage.HealthBucket)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckHealth_StorageDoesNotAnswer_IsLiveButNotReady()
    {
        await using var api = new ApiFactory(
            database.ApplicationConnectionString,
            settings: new Dictionary<string, string?> { ["Host:Storage:Endpoint"] = "http://127.0.0.1:1/", ["Host:Storage:Timeout"] = "00:00:01" },
            ports: HostPorts.Free());
        var client = api.ManagementClient();

        var live = await client.GetAsync("/health/live", Cancellation);
        var ready = await client.GetAsync("/health/ready", Cancellation);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}
