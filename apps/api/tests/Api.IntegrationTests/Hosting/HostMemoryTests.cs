using System.Diagnostics;
using Tenancy;

namespace Api.IntegrationTests.Hosting;

// Section 9, rule 11: a test host on the committed handler code leaves little memory behind when it stops. A host that compiled its
// handler code at runtime kept 26 to 41 MB of the process's resident memory; a host on the committed code keeps about 4 MB. The limit
// lies between the two. The class runs alone, after every other test, so no other host changes the process's memory while it measures.
// The case of the resident memory runs on Linux only (section 9, rule 15).
[Collection(typeof(RunsAlone))]
public sealed class HostMemoryTests(Database database)
{
    private const int Hosts = 6;

    private const long LimitPerHost = 10 * 1024 * 1024;

    private const long ManagedLimitPerHost = 5 * 1024 * 1024;

    public static bool OnLinux => OperatingSystem.IsLinux();

    [Fact(Skip = "The resident size measures freed memory on Linux only (section 9, rule 15).", SkipUnless = nameof(OnLinux))]
    public async Task StartAndStopHosts_OnTheCommittedHandlerCode_LeaveAtMostTenMegabytesEach()
    {
        await RunHostsAsync(1);
        var before = ResidentMemoryAfterACollection();

        await RunHostsAsync(Hosts);
        var perHost = (ResidentMemoryAfterACollection() - before) / Hosts;

        perHost.ShouldBeLessThan(LimitPerHost, $"Each stopped host kept {perHost / 1024 / 1024} MB of resident memory.");
    }

    // The managed heap after a full collection counts only what is still reachable, so this case runs on every system. Each host only
    // answers a request: a host that waited for messages stays reachable for the tracked session's timeout (section 9, rule 9).
    [Fact]
    public async Task StartAndStopHosts_ThatAnswerARequest_KeepAtMostFiveMegabytesOfManagedMemoryEach()
    {
        await AnswerARequestAsync(1);
        var before = ManagedMemoryAfterACollection();

        await AnswerARequestAsync(Hosts);
        var perHost = (ManagedMemoryAfterACollection() - before) / Hosts;

        perHost.ShouldBeLessThan(ManagedLimitPerHost, $"Each stopped host kept {perHost / 1024 / 1024} MB of managed memory.");
    }

    private static long ManagedMemoryAfterACollection()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    private static long ResidentMemoryAfterACollection()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        using var process = Process.GetCurrentProcess();
        return process.WorkingSet64;
    }

    private async Task AnswerARequestAsync(int count)
    {
        var testData = new TestData(database);
        for (var host = 0; host < count; host++)
        {
            await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
            using var tenants = await api.CreateClient(await testData.AddSystemAdminAsync(), secondFactor: true)
                .GetAsync(new Uri("/v1/platform/tenants", UriKind.Relative), TestContext.Current.CancellationToken);
        }
    }

    // Each host handles a command and the event it announces, as most tests do, and stops.
    private async Task RunHostsAsync(int count)
    {
        var testData = new TestData(database);
        for (var host = 0; host < count; host++)
        {
            await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
            var admin = api.CreateClient(await testData.AddSystemAdminAsync(), secondFactor: true);
            var tenant = await testData.AddTenantAsync(status: "Active");

            await api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant.Id));
        }
    }
}
