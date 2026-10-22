using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Api.IntegrationTests.Idempotency;

[RuntimeHandlerCode]
public sealed class IdempotencyBoundaryTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunCommand_AbandonedExpiredClaim_RunsAgainAndSavesTheResult()
    {
        await using var host = await PipelineHost.StartAsync(testHandlers: true);
        var client = host.CreateClient("user_abandoned");
        var key = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var body = new RecordIdempotentProbe(Guid.NewGuid().ToString());
        var gate = host.Services.GetRequiredService<IdempotencyGate>();
        gate.PausedValue = body.Value;
        gate.FailOnce(body.Value);
        var first = client.PostAsJsonAsync("/v1/idempotency/first", body, Cancellation);
        await gate.Entered.Task.WaitAsync(Cancellation);
        await using var scope = host.Services.CreateAsyncScope();
        var api = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var claim = await api.IdempotencyKeys.AsNoTracking().SingleAsync(row => row.Key == key, Cancellation);
        gate.Released.TrySetResult();
        (await first).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        claim.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
        api.IdempotencyKeys.Add(claim);
        await api.SaveChangesAsync(Cancellation);

        var recovered = await client.PostAsJsonAsync("/v1/idempotency/first", body, Cancellation);

        recovered.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM probes.probes WHERE value = '{body.Value}'")).ShouldBe(1);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM api.idempotency_keys WHERE key = '{key}' AND result IS NOT NULL AND locked_until IS NULL")).ShouldBe(1);
    }

    [Fact]
    public async Task RunCommand_LeaseExpiresWhileTheHandlerRuns_RemainsConflictUntilTheTransactionEnds()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await PipelineHost.StartAsync(configureServices: services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)), testHandlers: true);
        var client = host.CreateClient("user_expired");
        var key = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var body = new RecordIdempotentProbe(Guid.NewGuid().ToString());
        var gate = host.Services.GetRequiredService<IdempotencyGate>();
        gate.PausedValue = body.Value;
        var first = client.PostAsJsonAsync("/v1/idempotency/first", body, Cancellation);
        await gate.Entered.Task.WaitAsync(Cancellation);
        clock.Advance(TimeSpan.FromMinutes(2));
        HttpResponseMessage recovered;

        try
        {
            recovered = await client.PostAsJsonAsync("/v1/idempotency/first", body, Cancellation);
        }
        finally
        {
            gate.Released.TrySetResult();
        }

        recovered.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await first).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM probes.probes WHERE value = '{body.Value}'")).ShouldBe(1);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM api.idempotency_keys WHERE key = '{key}' AND result IS NOT NULL AND locked_until IS NULL")).ShouldBe(1);
    }

    [Fact]
    public async Task RunCommand_FinishedRequest_ReplaysTheBodyStatusAndLocation()
    {
        await using var host = await PipelineHost.StartAsync(testHandlers: true);
        var client = host.CreateClient("user_replay");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var body = new RecordIdempotentProbe(Guid.NewGuid().ToString());
        var first = await client.PostAsJsonAsync("/v1/idempotency/first", body, Cancellation);

        var replay = await client.PostAsJsonAsync("/v1/idempotency/first", body, Cancellation);

        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        replay.Headers.Location.ShouldBe(first.Headers.Location);
        (await replay.Content.ReadAsStringAsync(Cancellation)).ShouldBe(await first.Content.ReadAsStringAsync(Cancellation));
        replay.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM probes.probes WHERE value = '{body.Value}'")).ShouldBe(1);
    }

    [Theory]
    [InlineData("POST", "/v1/idempotency/changed")]
    [InlineData("POST", "/v1/idempotency-other/first")]
    [InlineData("PUT", "/v1/idempotency/first")]
    public async Task RunCommand_KeyReusedWithAnotherRouteOrMethod_IsUnprocessable(string method, string path)
    {
        await using var host = await PipelineHost.StartAsync(testHandlers: true);
        var client = host.CreateClient("user_route");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var body = new RecordIdempotentProbe(Guid.NewGuid().ToString());
        await client.PostAsJsonAsync("/v1/idempotency/first", body, Cancellation);
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(body) };

        var reused = await client.SendAsync(request, Cancellation);

        reused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM probes.probes WHERE value = '{body.Value}'")).ShouldBe(1);
    }

    [Fact]
    public async Task RunCommand_FirstRequestIsRunning_AnswersConflictWithoutWaiting()
    {
        await using var host = await PipelineHost.StartAsync(testHandlers: true);
        var client = host.CreateClient("user_busy");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var body = new RecordIdempotentProbe(Guid.NewGuid().ToString());
        var gate = host.Services.GetRequiredService<IdempotencyGate>();
        gate.PausedValue = body.Value;
        var first = client.PostAsJsonAsync("/v1/idempotency/first", body, Cancellation);
        await gate.Entered.Task.WaitAsync(Cancellation);

        HttpResponseMessage concurrent;
        HttpResponseMessage changed;
        try
        {
            concurrent = await client.PostAsJsonAsync("/v1/idempotency/first", body, Cancellation);
            changed = await client.PostAsJsonAsync("/v1/idempotency/first", new RecordIdempotentProbe("changed"), Cancellation);
        }
        finally
        {
            gate.Released.TrySetResult();
        }

        concurrent.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await concurrent.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString().ShouldBe("idempotency.key_in_progress");
        changed.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await changed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString().ShouldBe("idempotency.key_reused");
        (await first).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("/v1/idempotency/first", HttpStatusCode.InternalServerError)]
    [InlineData("/v1/idempotency-unavailable/first", HttpStatusCode.ServiceUnavailable)]
    public async Task RunCommand_ExceptionOrServerFailure_ReleasesTheClaimAndRollsBackTheBusinessWrite(string path, HttpStatusCode expected)
    {
        await using var host = await PipelineHost.StartAsync(testHandlers: true);
        var client = host.CreateClient("user_failure");
        var key = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var body = new RecordIdempotentProbe(Guid.NewGuid().ToString());
        host.Services.GetRequiredService<IdempotencyGate>().FailOnce(body.Value);

        var failed = await client.PostAsJsonAsync(path, body, Cancellation);

        failed.StatusCode.ShouldBe(expected);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM api.idempotency_keys WHERE key = '{key}'")).ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM probes.probes WHERE value = '{body.Value}'")).ShouldBe(0);
        (await client.PostAsJsonAsync(path, body, Cancellation)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM api.idempotency_keys WHERE key = '{key}' AND result IS NOT NULL AND locked_until IS NULL")).ShouldBe(1);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM probes.probes WHERE value = '{body.Value}'")).ShouldBe(1);
    }
}
