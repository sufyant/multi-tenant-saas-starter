using System.Diagnostics;
using System.Reflection;

namespace Api.ArchitectureTests;

// W10: the application loads the handler code that `codegen write` wrote to Internal/Generated, and the files are committed. Wolverine
// finds each type by its name and does not compare its code. It finds the handlers themselves in the committed registry. So code that
// is out of date runs old middleware, or leaves a new handler out, without an error. This test writes the code again, from the build
// of the host in each role, and compares it with the committed files: the role changes no code.
public sealed class HandlerCodeFileTests
{
    private const string GeneratedFolder = "src/Api/Internal/Generated";

    [Theory]
    [InlineData("all")]
    [InlineData("web")]
    [InlineData("worker")]
    public async Task WriteHandlerCode_CurrentHandlersInEachRole_IsTheCommittedCode(string role)
    {
        var written = await WriteHandlerCodeAsync(role);
        var committed = FilesIn(Path.Combine(Solution.Root, GeneratedFolder));

        var different = written.Keys.Union(committed.Keys)
            .Where(file => written.GetValueOrDefault(file) != committed.GetValueOrDefault(file))
            .Order();

        written.ShouldNotBeEmpty();
        different.ShouldBeEmpty($"The committed handler code in {GeneratedFolder} is out of date. Run `pnpm api:codegen` and add the folder to the commit.");
    }

    // Runs `codegen write` on the host's own build output, in a folder of its own: the command writes below its working directory.
    private static async Task<IReadOnlyDictionary<string, string>> WriteHandlerCodeAsync(string role)
    {
        var output = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var host = Path.Combine(
            Solution.Root, "src", Solution.Host, "bin",
            typeof(HandlerCodeFileTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration,
            Path.GetFileName(output),
            $"{Solution.Host}.dll");
        var folder = Directory.CreateTempSubdirectory("handler-code-").FullName;
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = folder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment = { ["Host__Role"] = role },
            };
            start.ArgumentList.Add(host);
            start.ArgumentList.Add("codegen");
            start.ArgumentList.Add("write");

            using var command = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start.");
            var standardOutput = command.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var standardError = command.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await command.WaitForExitAsync(TestContext.Current.CancellationToken);

            command.ExitCode.ShouldBe(0, $"codegen write failed:{Environment.NewLine}{await standardOutput}{await standardError}");
            return FilesIn(Path.Combine(folder, "Internal", "Generated"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // Each file by its path below the folder, with the line ends of the machine that wrote it made equal.
    private static Dictionary<string, string> FilesIn(string folder) =>
        Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(
                file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'),
                file => File.ReadAllText(file).ReplaceLineEndings("\n"))
            : [];
}
