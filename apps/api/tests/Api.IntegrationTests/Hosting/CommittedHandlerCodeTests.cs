using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Api.IntegrationTests.Hosting;

// Section 9, rule 11: a test policy's frame never runs on the committed handler code, so ApiFactory does not start a host that loads
// that code and carries such a frame, and neither does PipelineHost without its test handlers.
public sealed class CommittedHandlerCodeTests(Database database)
{
    [Fact]
    public async Task StartHost_ATestPolicyOnTheCommittedCode_DoesNotStart()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString, configureServices: services =>
            services.ConfigureWolverine(options => options.Policies.Add<IdempotencyCommitFailurePolicy>()));

        var start = Should.Throw<InvalidOperationException>(() => api.CreateClient());

        start.Message.ShouldContain(nameof(RuntimeHandlerCode.GenerateHandlerCodeAtRuntime));
    }

    [Fact]
    public async Task StartPipelineHost_ATestPolicyOnTheCommittedCode_DoesNotStart()
    {
        var start = await Should.ThrowAsync<InvalidOperationException>(() => PipelineHost.StartAsync(configureServices: services =>
            services.ConfigureWolverine(options => options.Policies.Add<IdempotencyCommitFailurePolicy>())));

        start.Message.ShouldContain(nameof(RuntimeHandlerCode.GenerateHandlerCodeAtRuntime));
    }
}
