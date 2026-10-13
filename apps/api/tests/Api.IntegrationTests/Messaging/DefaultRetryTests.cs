using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Messaging;

public sealed class DefaultRetryTests(Database database)
{
    [Theory]
    [InlineData("all")]
    [InlineData("worker")]
    public async Task HandleQueuedMessage_AlwaysFails_RetriesThreeTimesThenDeadLetters(string role)
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await database.CreateProbesAsync(name);
        await using var host = await PipelineHost.StartAsync(new Dictionary<string, string?>
        {
            ["Host:Role"] = role,
            ["ConnectionStrings:Database"] = database.ConnectionStringFor(Tenancy.DatabaseRoles.Application, name),
        });
        var command = new RetryProbe(Guid.NewGuid());

        await host.Host.MessageBus().SendAsync(command);
        await Waiting.UntilAsync(async () => await database.ScalarAsync<long>(
            $"SELECT count(*) FROM wolverine.wolverine_dead_letters WHERE message_type = 'Api.IntegrationTests.Fixtures.RetryProbe' AND position(convert_to('{command.Id}', 'UTF8') in body) > 0", database: name) == 1);

        host.Services.GetRequiredService<RetryAttempts>().Count(command.Id).ShouldBe(4);
    }

    [Fact]
    public async Task HandleQueuedMessage_MessageSpecificRule_RunsBeforeTheGlobalRule()
    {
        await using var host = await PipelineHost.StartAsync();
        var command = new SpecificRetryProbe(Guid.NewGuid());

        await host.Host.TrackActivity().DoNotAssertOnExceptionsDetected().SendMessageAndWaitAsync(command);

        host.Services.GetRequiredService<RetryAttempts>().Count(command.Id).ShouldBe(2);
    }

    [Fact]
    public async Task InvokeCommand_DefaultRule_DoesNotRetryTheHttpCommand()
    {
        await using var host = await PipelineHost.StartAsync(new Dictionary<string, string?> { ["Host:Role"] = "web" });
        var command = new RetryProbe(Guid.NewGuid());

        var response = await host.CreateClient("user_retry").PostAsJsonAsync("/v1/retry-probes", command, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        host.Services.GetRequiredService<RetryAttempts>().Count(command.Id).ShouldBe(1);
    }

    [Fact]
    public async Task InvokeCommand_UnrestrictedGlobalRule_RetriesTheHttpCommand()
    {
        await using var host = await PipelineHost.StartAsync(configureServices: services => services.ConfigureWolverine(options =>
            options.OnAnyException().RetryWithCooldown(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)));
        var command = new RetryProbe(Guid.NewGuid());

        var response = await host.CreateClient("user_unrestricted_retry").PostAsJsonAsync("/v1/retry-probes", command, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        host.Services.GetRequiredService<RetryAttempts>().Count(command.Id).ShouldBe(4);
    }
}
