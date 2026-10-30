using System.Globalization;
using System.Text.RegularExpressions;

namespace Api.IntegrationTests.Hosting;

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
        var result = await ApiProcess.RunAsync(SettingsFor(role, HostPorts.Free()));

        result.Exited.ShouldBeTrue(result.Output);
        result.ExitCode.ShouldBe(1, result.Output);
        result.Output.ShouldContain("Failed to connect to 127.0.0.1:1");
        Regex.Count(result.Output, "Hosting failed to start").ShouldBe(1, result.Output);
        result.Output.ShouldNotContain("Unhandled exception");
    }

    // The host reads some settings while it is composed, before it has a logger. A value it cannot read stops the process the same way,
    // and the error is written once.
    [Theory]
    [InlineData("web")]
    [InlineData("worker")]
    public async Task StartProcess_WithASettingThatCannotBeRead_WritesTheFailureOnceAndExitsWithOne(string role)
    {
        var settings = new Dictionary<string, string>(SettingsFor(role, HostPorts.Free())) { ["Host__ManagementPort"] = "abc" };

        var result = await ApiProcess.RunAsync(settings);

        result.Exited.ShouldBeTrue(result.Output);
        result.ExitCode.ShouldBe(1, result.Output);
        Regex.Count(result.Output, "Failed to convert configuration value 'abc' at 'Host:ManagementPort'").ShouldBe(1, result.Output);
        result.Output.ShouldNotContain("Unhandled exception");
    }

    private static Dictionary<string, string> SettingsFor(string role, HostPorts ports) => new()
    {
        ["DOTNET_ENVIRONMENT"] = "Development",
        ["Host__Role"] = role,
        ["ConnectionStrings__Database"] = ApiProcess.HostThatDoesNotAnswer,
        ["Host__Authentication__Issuer"] = TestTokens.Issuer,
        ["ControlPlane__Clerk__SecretKey"] = "sk_test_unused",
        ["ControlPlane__Invitations__AcceptUrl"] = ApiFactory.AcceptUrl,
        ["Notifications__Links__WebUrl"] = ApiFactory.WebUrl,
        ["Notifications__Links__PlatformUrl"] = ApiFactory.PlatformUrl,
        ["ControlPlane__SystemAdmins__RootEmails__0"] = ApiFactory.RootEmail,
        ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{ports.Public}",
        ["Host__ManagementPort"] = ports.Management.ToString(CultureInfo.InvariantCulture),
    };
}
