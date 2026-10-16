using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Api.IntegrationTests.Idempotency;

public sealed class IdempotencyTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateTenant_CommitFailsAfterTheResultWrite_RollsBackTheTenantAndResult()
    {
        var key = Guid.NewGuid().ToString();
        var failure = new IdempotencyCommitFailure { Key = key };
        await using var api = new ApiFactory(database.ApplicationConnectionString, configureServices: services =>
        {
            services.AddSingleton(failure);
            services.ConfigureWolverine(options => options.Policies.Add<IdempotencyCommitFailurePolicy>());
        });
        var client = api.CreateClient(await new Catalog(database).AddSystemAdminAsync(), secondFactor: true);
        var slug = $"rollback-{Guid.NewGuid():N}";

        var failed = await client.CreateTenantAsync(new { name = "Acme", slug, ownerEmail = "owner@example.com" }, key);

        failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        failure.SawTheSavedResult.ShouldBeTrue();
        (await database.ScalarAsync<long>($"SELECT count(*) FROM api.idempotency_keys WHERE key = '{key}'")).ShouldBe(0);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(0);
    }

    [Fact]
    public async Task CreateTenant_FirstRequest_SavesTheHandlerResultWithoutTheRequestBody()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);
        var admin = await new Catalog(database).AddSystemAdminAsync();
        var client = api.CreateClient(admin, secondFactor: true);
        var key = Guid.NewGuid().ToString();

        var response = await client.CreateTenantAsync(new { name = "Acme", slug = $"first-{Guid.NewGuid():N}", ownerEmail = "private@example.com" }, key);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM api.idempotency_keys WHERE subject = '{admin}' AND key = '{key}' AND locked_until IS NULL AND result IS NOT NULL AND length(request_hash) = 64")).ShouldBe(1);
        (await database.ScalarAsync<string>($"SELECT result FROM api.idempotency_keys WHERE subject = '{admin}' AND key = '{key}'"))!.ShouldNotContain("private@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not an email")]
    public async Task CreateTenant_ValidationFails_DoesNotSaveAndCorrectedRetryRuns(string ownerEmail)
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);
        var client = api.CreateClient(await new Catalog(database).AddSystemAdminAsync(), secondFactor: true);
        var key = Guid.NewGuid().ToString();
        var slug = $"validation-{Guid.NewGuid():N}";

        var invalid = await client.CreateTenantAsync(new { name = "Acme", slug, ownerEmail }, key);
        var corrected = await client.CreateTenantAsync(new { name = "Acme", slug, ownerEmail = "owner@example.com" }, key);

        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        corrected.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM api.idempotency_keys WHERE key = '{key}'")).ShouldBe(1);
    }

    [Fact]
    public async Task CreateTenant_DomainValidationFails_DoesNotSaveAndCorrectedRetryRuns()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);
        var client = api.CreateClient(await new Catalog(database).AddSystemAdminAsync(), secondFactor: true);
        var key = Guid.NewGuid().ToString();
        var slug = $"domain-{Guid.NewGuid():N}";

        var invalid = await client.CreateTenantAsync(new { name = "", slug, ownerEmail = "owner@example.com" }, key);
        var corrected = await client.CreateTenantAsync(new { name = "Acme", slug, ownerEmail = "owner@example.com" }, key);

        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        corrected.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await database.ScalarAsync<long>($"SELECT count(*) FROM api.idempotency_keys WHERE key = '{key}'")).ShouldBe(1);
    }

    [Fact]
    public async Task CreateTenant_BusinessRejection_ReplaysTheExactStatusAndBody()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);
        var catalog = new Catalog(database);
        var client = api.CreateClient(await catalog.AddSystemAdminAsync(), secondFactor: true);
        var tenant = await catalog.AddTenantAsync();
        var key = Guid.NewGuid().ToString();
        var body = new { name = "Acme", slug = tenant.Slug, ownerEmail = "owner@example.com" };
        var first = await client.CreateTenantAsync(body, key);

        var replay = await client.CreateTenantAsync(body, key);

        replay.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        replay.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await replay.Content.ReadAsStringAsync(Cancellation)).ShouldBe(await first.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task DescribeEndpoint_IdempotencyRequired_DeclaresTheRequiredHeader()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);

        var document = await api.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json", Cancellation);

        var header = document.GetProperty("paths").GetProperty(TenantRequests.Path).GetProperty("post").GetProperty("parameters")
            .EnumerateArray().Single(parameter => parameter.GetProperty("name").GetString() == "Idempotency-Key");
        header.GetProperty("required").GetBoolean().ShouldBeTrue();
    }
}
