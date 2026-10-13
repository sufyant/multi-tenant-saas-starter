using System.Diagnostics;

namespace Api.IntegrationTests.Fixtures;

// Runs the host the way a deployment does, as `dotnet Api.dll`, with the given arguments and environment. The host is built into the
// test's output folder, so the command runs from there. A process that still runs after the time limit is stopped and reported as
// not exited: a process that fails must stop on its own.
internal static class ApiProcess
{
    public static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(30);

    // Nothing listens on port 1, so the connection is refused at once.
    public const string HostThatDoesNotAnswer = "Host=127.0.0.1;Port=1;Database=api;Username=nobody;Password=none";

    public static async Task<ApiProcessResult> RunAsync(IReadOnlyDictionary<string, string> environment, params string[] arguments)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("Api.dll");
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        var testCancelled = TestContext.Current.CancellationToken;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The host process did not start.");
        var output = process.StandardOutput.ReadToEndAsync(testCancelled);
        var error = process.StandardError.ReadToEndAsync(testCancelled);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(testCancelled);
        limit.CancelAfter(TimeLimit);
        var exited = true;
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException) when (!testCancelled.IsCancellationRequested)
        {
            exited = false;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(testCancelled);
        }

        return new(exited, process.ExitCode, await output + await error);
    }
}

internal sealed record ApiProcessResult(bool Exited, int ExitCode, string Output);
