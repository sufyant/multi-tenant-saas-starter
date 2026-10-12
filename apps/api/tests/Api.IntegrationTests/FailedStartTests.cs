using System.Globalization;
using System.Text.RegularExpressions;

namespace Api.IntegrationTests;

// A web or worker process that cannot reach its database does not start. The host logs the failure once ("Hosting failed to start"),
// and the process exits on its own with the code 1, never through the runtime's abort: as PID 1 in a container, an abort can spin
// forever (Twelve-Factor IX; Nygard, "Fail Fast").
public sealed class FailedStartTests
{
    [Theory]
    [InlineData("web")]
    [InlineData("worker")]
    public async Task StartProcess_OnAHostThatDoesNotAnswer_LogsTheFailureOnceAndExitsWithOne(string role)
    {
        var ports = HostPorts.Free();

        var result = await ApiProcess.RunAsync(new Dictionary<string, string>
        {
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["Host__Role"] = role,
            ["ConnectionStrings__Database"] = ApiProcess.HostThatDoesNotAnswer,
            ["Authentication__Clerk__Issuer"] = TestTokens.Issuer,
            ["ControlPlane__Clerk__SecretKey"] = "sk_test_unused",
            ["ControlPlane__Invitations__AcceptUrl"] = ApiFactory.AcceptUrl,
            ["ControlPlane__FirstSystemAdminEmail"] = ApiFactory.FirstSystemAdminEmail,
            ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{ports.Public}",
            ["Host__ManagementPort"] = ports.Management.ToString(CultureInfo.InvariantCulture),
        });

        result.Exited.ShouldBeTrue(result.Output);
        result.ExitCode.ShouldBe(1, result.Output);
        result.Output.ShouldContain("Failed to connect to 127.0.0.1:1");
        Regex.Count(result.Output, "Hosting failed to start").ShouldBe(1, result.Output);
        result.Output.ShouldNotContain("Unhandled exception");
    }
}
