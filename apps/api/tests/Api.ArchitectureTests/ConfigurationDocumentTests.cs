using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.ArchitectureTests;

// Smoke findings 8 and 11: docs/configuration.md has a row for every key the application binds. The keys are read from the composed
// application: every options class of the solution that is bound to a configuration section, and the settable properties under it.
// The example file lists the same keys, and the Default column holds the code's defaults (audit B12, B13).
public sealed partial class ConfigurationDocumentTests
{
    // The host reads its ASPNETCORE_ variables from the environment, not from a configuration file.
    private const string EnvironmentOnlyPrefix = "ASPNETCORE_";

    private const string FromAppSettings = "(`appsettings.json`)";

    [Fact]
    public async Task DocumentConfiguration_EveryKeyBoundInCode_HasARow()
    {
        var bound = await KeysBoundInCodeAsync();

        var undocumented = bound.Except(DocumentedKeys());

        bound.ShouldContain("Host:Role");
        undocumented.ShouldBeEmpty();
    }

    [Fact]
    public void ExampleConfiguration_ItsKeys_EqualTheDocumentedKeys()
    {
        var documented = DocumentedKeys().Where(key => !key.StartsWith(EnvironmentOnlyPrefix, StringComparison.Ordinal));

        var example = ExampleKeys().Select(key => AsDocumentedKey(key, DocumentedKeys())).Distinct();

        example.Order().ShouldBe(documented.Order());
    }

    [Fact]
    public async Task DocumentConfiguration_DefaultOfEachBoundKey_EqualsTheDefaultInCode()
    {
        var inCode = await DefaultsInCodeAsync();
        var documented = DocumentedDefaults().Where(row => inCode.ContainsKey(row.Key) && !row.Value.Contains(FromAppSettings, StringComparison.Ordinal));

        var differences = documented.Where(row => DocumentedValue(row.Value) != inCode[row.Key]).Select(row => $"{row.Key}: {row.Value} in the document, {inCode[row.Key]} in code");

        documented.ShouldNotBeEmpty();
        differences.ShouldBeEmpty();
    }

    [Fact]
    public void DocumentConfiguration_DefaultFromAppSettings_EqualsTheValueInAppSettings()
    {
        var appSettings = new ConfigurationBuilder().AddJsonFile(Path.Combine(Solution.Root, "src", "Api", "appsettings.json")).Build();
        var documented = DocumentedDefaults().Where(row => row.Value.Contains(FromAppSettings, StringComparison.Ordinal) && !IsPattern(row.Key));

        var differences = documented.Where(row => DocumentedValue(row.Value) != appSettings[row.Key]).Select(row => $"{row.Key}: {row.Value} in the document, {appSettings[row.Key]} in appsettings.json");

        documented.ShouldNotBeEmpty();
        differences.ShouldBeEmpty();
    }

    // B14: each deployed environment gets every key that the document marks as required, and only keys that it documents. The
    // migration's own connection goes to the migrate command alone (R3).
    [Fact]
    public void RailwayConfiguration_ItsVariables_SetEveryRequiredKey()
    {
        var railway = File.ReadAllText(Path.Combine(Solution.Root, "..", "..", ".railway", "railway.ts"));
        var set = RailwayVariable().Matches(railway).Select(match => KeyOf(match.Groups["name"].Value)).ToHashSet();
        var required = DocumentedRows().Where(row => row.Required.StartsWith("Required", StringComparison.Ordinal) && !row.Required.Contains("`migrate`", StringComparison.Ordinal)).Select(row => row.Key);

        required.Except(set).ShouldBeEmpty();
        set.Except(DocumentedKeys()).ShouldBeEmpty();
    }

    // A variable such as Authentication__Clerk__AuthorizedParties__0 is the key Authentication:Clerk:AuthorizedParties.
    private static string KeyOf(string variable) => ListIndex().Replace(variable, "").Replace("__", ":", StringComparison.Ordinal);

    private static IEnumerable<(string Key, string Required)> DocumentedRows() =>
        File.ReadLines(Path.Combine(Solution.Root, "docs", "configuration.md"))
            .Select(line => TableRow().Match(line))
            .Where(match => match.Success)
            .Select(match => (match.Groups["key"].Value, match.Groups["required"].Value.Trim()));

    // The first cell of each table row, when it is a key in backticks.
    private static HashSet<string> DocumentedKeys() => [.. DocumentedDefaults().Keys];

    // The key of each table row and its Default cell.
    private static Dictionary<string, string> DocumentedDefaults() =>
        File.ReadLines(Path.Combine(Solution.Root, "docs", "configuration.md"))
            .Select(line => TableRow().Match(line))
            .Where(match => match.Success)
            .ToDictionary(match => match.Groups["key"].Value, match => match.Groups["default"].Value.Trim());

    // A Default cell as the code would print it: none, or the values in backticks, apart from a note on where they come from.
    private static string DocumentedValue(string cell) =>
        cell == "none" ? "none" : string.Join(", ", BacktickValue().Matches(cell.Replace(FromAppSettings, "", StringComparison.Ordinal)).Select(match => match.Groups["value"].Value));

    // A documented key such as Serilog:MinimumLevel:Override:<category> stands for every key in its place.
    private static bool IsPattern(string key) => key.Contains('<', StringComparison.Ordinal);

    private static string AsDocumentedKey(string key, IEnumerable<string> documented) =>
        documented.Where(IsPattern).FirstOrDefault(pattern => Regex.IsMatch(key, $"^{Placeholder().Replace(Regex.Escape(pattern), "[^:]+")}$")) ?? key;

    // The keys of appsettings.Example.json: a list is one key, with no index.
    private static IEnumerable<string> ExampleKeys()
    {
        using var example = JsonDocument.Parse(File.ReadAllText(Path.Combine(Solution.Root, "appsettings.Example.json")));
        return [.. KeysOf(example.RootElement, prefix: null)];
    }

    private static IEnumerable<string> KeysOf(JsonElement element, string? prefix) =>
        element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().SelectMany(property => KeysOf(property.Value, prefix is null ? property.Name : $"{prefix}:{property.Name}"))
            : [prefix!];

    private static async Task<HashSet<string>> KeysBoundInCodeAsync() => [.. (await DefaultsInCodeAsync()).Keys];

    // Each bound key and the value a new options class holds for it, as the document writes it.
    private static async Task<Dictionary<string, string>> DefaultsInCodeAsync()
    {
        List<Type> optionsClasses = [];
        await using var application = new ComposedApplication(configure: builder =>
            builder.ConfigureTestServices(services => optionsClasses.AddRange(OptionsClassesBoundIn(services))));
        var services = application.Services;

        return optionsClasses
            .SelectMany(options => SectionsOf(options, services)
                .SelectMany(section => DefaultsOf(Activator.CreateInstance(options, nonPublic: true)!).Select(setting => ($"{section}:{setting.Key}", setting.Value))))
            .ToDictionary(setting => setting.Item1, setting => setting.Value);
    }

    // Every options class of the solution that is bound to configuration: binding registers a change token source for it.
    private static List<Type> OptionsClassesBoundIn(IServiceCollection services) =>
    [
        .. services
            .Select(service => service.ServiceType)
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IOptionsChangeTokenSource<>))
            .Select(type => type.GetGenericArguments()[0])
            .Where(options => Solution.AllProjects.Contains(options.Assembly.GetName().Name!))
            .Distinct(),
    ];

    // BindConfiguration registers a step that configures the options from the configuration; the section it was given is the
    // configSectionPath the step's delegate captured.
    private static IEnumerable<string> SectionsOf(Type options, IServiceProvider services) =>
        services.GetServices(typeof(IConfigureOptions<>).MakeGenericType(options))
            .Where(step => step!.GetType().IsGenericType && step.GetType().GetGenericTypeDefinition() == typeof(ConfigureNamedOptions<,>))
            .Where(step => step!.GetType().GetGenericArguments()[1] == typeof(IConfiguration))
            .Select(step => ((Delegate)step!.GetType().GetProperty("Action")!.GetValue(step)!).Target)
            .Select(closure => closure?.GetType().GetField("configSectionPath")?.GetValue(closure) as string)
            .OfType<string>();

    // The paths of the settable values and lists of an options class, and of the classes it holds, with their defaults.
    private static IEnumerable<KeyValuePair<string, string>> DefaultsOf(object options) =>
        options.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod?.IsPublic == true)
            .SelectMany(property => IsValue(property.PropertyType)
                ? property.SetMethod?.IsPublic == true ? [KeyValuePair.Create(property.Name, Format(DefaultOf(options, property)))] : []
                : DefaultsOf(property.GetValue(options)!).Select(setting => KeyValuePair.Create($"{property.Name}:{setting.Key}", setting.Value)));

    // A list that is unset until configured has its default in a static field named after it (section 7, "Configuration").
    private static object? DefaultOf(object options, PropertyInfo property) =>
        property.GetValue(options)
        ?? options.GetType().GetField($"Default{property.Name}", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

    private static string Format(object? value) => value switch
    {
        null => "none",
        string text => text,
        TimeSpan time => time.ToString("c", CultureInfo.InvariantCulture),
        IEnumerable items when items.Cast<object>().Any() => string.Join(", ", items.Cast<object>().Select(Format)),
        IEnumerable => "none",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()!,
    };

    private static bool IsValue(Type type) =>
        type.IsValueType || type == typeof(string) || type == typeof(Uri) || typeof(IEnumerable).IsAssignableFrom(type);

    [GeneratedRegex(@"^\| `(?<key>[^`]+)` \| (?<required>[^|]+) \| (?<default>[^|]+) \|")]
    private static partial Regex TableRow();

    // A name in the environment variable form, in quotes or as an object key: "ControlPlane__Clerk__SecretKey", Host__Role:.
    [GeneratedRegex(@"(?<=[""\s])(?<name>[A-Za-z]+(?:__[A-Za-z0-9]+)+)(?=""|:)")]
    private static partial Regex RailwayVariable();

    [GeneratedRegex("__[0-9]+$")]
    private static partial Regex ListIndex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Placeholder();

    [GeneratedRegex("`(?<value>[^`]+)`")]
    private static partial Regex BacktickValue();
}
