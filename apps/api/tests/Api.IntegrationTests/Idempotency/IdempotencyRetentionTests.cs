using Api.Idempotency;
using Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Api.IntegrationTests.Idempotency;

public sealed class IdempotencyRetentionTests(Database database)
{
    [Fact]
    public async Task StartApplication_RetentionLessThanADay_FailsValidation()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString, settings: new Dictionary<string, string?>
        {
            ["Api:IdempotencyKeyRetention"] = "23:59:59",
        });

        var start = () => api.CreateClient();

        start.ShouldThrow<Exception>().Message.ShouldContain("Api:IdempotencyKeyRetention");
    }

    [Theory]
    [InlineData(24)]
    [InlineData(48)]
    public async Task CleanKeys_WorkerClockAdvances_RemovesOnlyExpiredUnlockedRows(int retentionHours)
    {
        var clock = new FakeTimeProvider();
        await using var host = await PipelineHost.StartAsync(
            new Dictionary<string, string?> { ["Host:Role"] = "worker", ["Api:IdempotencyKeyRetention"] = TimeSpan.FromHours(retentionHours).ToString("c", System.Globalization.CultureInfo.InvariantCulture) },
            services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        var subject = Guid.NewGuid().ToString();
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        context.IdempotencyKeys.AddRange(
            Row(subject, "old", clock.GetUtcNow().AddHours(-retentionHours - 1)),
            Row(subject, "recent", clock.GetUtcNow().AddHours(-retentionHours + 2)),
            Row(subject, "active", clock.GetUtcNow().AddHours(-retentionHours - 1), clock.GetUtcNow().AddHours(2)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromHours(1));
        await Waiting.UntilAsync(async () => !await context.IdempotencyKeys.AnyAsync(row => row.Subject == subject && row.Key == "old", TestContext.Current.CancellationToken));

        (await context.IdempotencyKeys.AsNoTracking().Where(row => row.Subject == subject).Select(row => row.Key).OrderBy(key => key)
            .ToArrayAsync(TestContext.Current.CancellationToken)).ShouldBe(["active", "recent"]);
    }

    [Fact]
    public async Task CleanKeys_WebClockAdvances_KeepsTheRows()
    {
        var clock = new FakeTimeProvider();
        await using var host = await PipelineHost.StartAsync(
            new Dictionary<string, string?> { ["Host:Role"] = "web" },
            services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        var subject = Guid.NewGuid().ToString();
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        context.IdempotencyKeys.Add(Row(subject, "old", clock.GetUtcNow().AddHours(-25)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromHours(1));

        (await context.IdempotencyKeys.CountAsync(row => row.Subject == subject, TestContext.Current.CancellationToken)).ShouldBe(1);
        host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().Select(service => service.GetType().Name).ShouldNotContain("IdempotencyCleanup");
    }

    private static IdempotencyKey Row(string subject, string key, DateTimeOffset createdAt, DateTimeOffset? lockedUntil = null) => new()
    {
        Subject = subject, Key = key, Method = "POST", Route = "/test", RequestHash = new string('0', 64),
        CreatedAt = createdAt, Lease = Guid.NewGuid(), LockedUntil = lockedUntil,
    };
}
