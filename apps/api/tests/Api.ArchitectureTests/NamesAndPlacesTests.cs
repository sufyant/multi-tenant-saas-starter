using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Api.ArchitectureTests;

// Section "Names and places": the names and folders the rules fix, read from the compiled code and the source tree.
public sealed partial class NamesAndPlacesTests
{
    private const string HostIntegrationTests = $"{Solution.Host}.IntegrationTests";

    private static readonly string[] CatchAllFolders = ["Helpers", "Utils", "Utilities", "Common", "Misc"];

    private static readonly string[] BuildOutput = ["bin", "obj"];

    private static readonly string[] ResourceLayers = ["Application", "Domain"];

    private static readonly string[] Audiences = ["Platform", "Me", "Tenant"];

    // Persistence holds Migrations, where EF Core's tool expects them, and Lists, the search and the sort that the query classes share (N8).
    private static readonly string[] PersistencePieces = ["Migrations", "Lists"];

    public static TheoryData<string> ModuleDbContextNames => [.. ModuleDbContexts.Types().Select(type => type.FullName!)];

    public static TheoryData<string> ModuleNames => [.. Solution.Modules];

    public static TheoryData<string> UnitTestProjects => [.. Solution.TestProjects.Where(project => project.EndsWith(".UnitTests", StringComparison.Ordinal))];

    public static TheoryData<string> ModuleIntegrationTestProjects =>
        [.. Solution.Modules.Select(module => $"{module}.IntegrationTests").Where(Solution.TestProjects.Contains)];

    // N4: a module's schema is its name in snake case, so the schema says which module owns a table.
    [Theory]
    [MemberData(nameof(ModuleDbContextNames))]
    public void NameSchemas_ModuleDbContext_UsesTheModuleNameInSnakeCase(string contextName)
    {
        var contextType = ModuleDbContexts.Types().Single(type => type.FullName == contextName);
        using var context = ModuleDbContexts.CreateAsDotnetEfDoes(contextType);
        var module = contextType.Assembly.GetName().Name!.Replace(".Infrastructure", "", StringComparison.Ordinal);

        var schema = context.Model.GetDefaultSchema();

        schema.ShouldBe(SnakeCase(module));
    }

    // N6: a permission is audience.resource.action. The audience is platform or tenant; the me audience has no permissions.
    [Fact]
    public void NamePermissions_EveryPermission_IsAudienceResourceAction()
    {
        var permissions = PermissionLists()
            .SelectMany(list => list.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        var misnamed = permissions.Where(permission => !AudienceResourceAction().IsMatch(permission));

        permissions.ShouldNotBeEmpty();
        misnamed.ShouldBeEmpty();
    }

    // N7t: the OpenAPI document builds its schema names from the type names, so two requests or responses of one name would clash.
    [Fact]
    public void NameApiTypes_RequestsAndResponses_AreUniqueAcrossTheApi()
    {
        var types = Solution.LayerOfEveryModule("Api").Append(Solution.Host)
            .SelectMany(project => Solution.Load(project).GetTypes())
            .Where(type => !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal) || type.Name.EndsWith("Response", StringComparison.Ordinal))
            .ToList();

        var clashes = types
            .GroupBy(type => type.Name)
            .Where(name => name.Count() > 1)
            .Select(name => $"{name.Key}: {string.Join(", ", name.Select(type => type.FullName))}");

        types.ShouldNotBeEmpty();
        clashes.ShouldBeEmpty();
    }

    // N8: a folder is named by what it holds. A catch-all name says nothing, so code piles up in it.
    [Fact]
    public void NameFolders_EveryProject_HasNoCatchAllFolder()
    {
        var catchAll = Solution.SolutionProjects
            .SelectMany(FoldersOf)
            .Where(folder => CatchAllFolders.Contains(Path.GetFileName(folder), StringComparer.OrdinalIgnoreCase))
            .Select(folder => Path.GetRelativePath(Solution.Root, folder));

        catchAll.ShouldBeEmpty();
    }

    // N10: a folder becomes a namespace, and a namespace hides a type of the same name (CS0118). No folder has the name of a type
    // that its project sees; a use case folder may have its action's name, because the role suffix gives every type a longer name.
    [Fact]
    public void NameFolders_EveryProject_HasNoFolderNamedLikeATypeItSees()
    {
        var clashes = Solution.SolutionProjects
            .SelectMany(project => FoldersOf(project).Select(folder => (Project: project, Folder: Path.GetFileName(folder))))
            .Where(folder => TypeNamesSeenBy(folder.Project).Contains(folder.Folder))
            .Select(folder => $"{folder.Project}: {folder.Folder}");

        clashes.ShouldBeEmpty();
    }

    // N9: a test project is {Project}.{Kind}Tests, where Project is a production project or a module of the solution.
    [Fact]
    public void NameTestProjects_EveryTestProject_IsProjectDotKindTests()
    {
        var testProjects = Solution.SolutionProjects.Where(project => project.EndsWith("Tests", StringComparison.Ordinal)).ToList();

        var misnamed = testProjects.Where(project => !IsProjectDotKindTests(project));

        testProjects.ShouldNotBeEmpty();
        misnamed.ShouldBeEmpty();
    }

    // N9: a unit test class is named by the class it tests, and sits in the folder of that class: TenantTests holds the rules of the
    // Tenant aggregate, in Tenants/.
    [Theory]
    [MemberData(nameof(UnitTestProjects))]
    public void NameTestClasses_UnitTestClass_IsNamedAndPlacedByTheClassItTests(string project)
    {
        var tested = TestedTypesOf(project).ToLookup(type => type.Name);
        var classes = TestClassesOf(project).ToList();

        var misplaced = classes
            .Where(test => !test.Name.EndsWith("Tests", StringComparison.Ordinal)
                || !tested[test.Name[..^"Tests".Length]].Any(type => FolderOf(type) == FolderOf(test)))
            .Select(test => test.FullName!);

        classes.ShouldNotBeEmpty();
        misplaced.ShouldBeEmpty();
    }

    // N9: in Api.IntegrationTests, each test of an endpoint's use case sits in the one class of that use case, {UseCase}Tests, in the
    // audience and resource folder of the endpoint (Platform/Tenants/CreateTenantTests). The use case is the first part of the test's
    // name. A test of a feature that several use cases share, such as the tenant door, names no use case first.
    [Fact]
    public void PlaceTests_TestOfAnEndpoint_SitsInTheClassOfItsUseCaseInTheFolderOfItsEndpoints()
    {
        var folders = EndpointFolders();
        var tests = TestMethodsOf(HostIntegrationTests)
            .Where(test => Audiences.Any(audience => $"{test.DeclaringType!.Namespace}.".StartsWith($"{HostIntegrationTests}.{audience}.", StringComparison.Ordinal)))
            .Where(test => folders.ContainsKey(UseCaseOf(test)))
            .ToList();

        var misplaced = tests
            .Where(test => test.DeclaringType!.FullName != $"{HostIntegrationTests}.{folders[UseCaseOf(test)]}.{UseCaseOf(test)}Tests")
            .Select(test => $"{test.DeclaringType!.FullName}.{test.Name}");

        tests.ShouldNotBeEmpty();
        misplaced.ShouldBeEmpty();
    }

    // N9: in a module's integration test project, each test of a use case sits in the one class of that use case, {UseCase}Tests, in the
    // folder of its resource in Application (Tenants/ActivateTenantTests). A class named by a class of the module, such as
    // TenantRepositoryTests, tests that class.
    [Theory]
    [MemberData(nameof(ModuleIntegrationTestProjects))]
    public void PlaceTests_TestOfAUseCase_SitsInTheClassOfItsUseCaseInTheFolderOfItsResource(string project)
    {
        var module = project[..^".IntegrationTests".Length];
        var resources = Handlers()
            .Where(handler => handler.Type.Assembly == Solution.Load($"{module}.Application") && !handler.Type.IsSubclassOf(typeof(Wolverine.Saga)))
            .Select(handler => handler.Type)
            .Distinct()
            .ToDictionary(handler => handler.Name[..^"Handler".Length], handler => handler.Namespace!.Split('.')[2]);
        var classTests = Solution.ProjectsOf(module).SelectMany(tested => Solution.Load(tested).GetTypes()).Select(type => $"{type.Name}Tests").ToHashSet();
        var tests = TestMethodsOf(project)
            .Where(test => resources.ContainsKey(UseCaseOf(test)) && !classTests.Contains(test.DeclaringType!.Name))
            .ToList();

        var misplaced = tests
            .Where(test => test.DeclaringType!.FullName != $"{project}.{resources[UseCaseOf(test)]}.{UseCaseOf(test)}Tests")
            .Select(test => $"{test.DeclaringType!.FullName}.{test.Name}");

        tests.ShouldNotBeEmpty();
        misplaced.ShouldBeEmpty();
    }

    // N8, rule 1: one type in each file, and the file has the type's name (StyleCop SA1402, SA1649). A generated file adds .g. A
    // type at the top of a file-scoped namespace starts its line; a nested type is indented.
    [Fact]
    public void NameFiles_EveryModuleSourceFile_DeclaresOneTypeOfItsName()
    {
        var files = ModuleSourceFiles().ToList();

        var misnamed = files
            .Select(file => (File: file, Types: TopLevelType().Matches(File.ReadAllText(file)).Select(match => match.Groups["name"].Value).ToList()))
            .Where(file => file.Types is not [var type] || type != Path.GetFileNameWithoutExtension(file.File).Replace(".g", "", StringComparison.Ordinal))
            .Select(file => $"{Path.GetRelativePath(Solution.Root, file.File)}: {string.Join(", ", file.Types)}");

        files.ShouldNotBeEmpty();
        misnamed.ShouldBeEmpty();
    }

    // N8, rule 2: a message names its role. An integration event in Contracts is {PastTense}Event, a saga's reply {Work}Reply (N3); in
    // Application a command is {Action}Command, a query {Action}Query, and a saga's timeout {Wait}TimedOut (N3).
    [Fact]
    public void NameMessages_EveryHandledMessage_EndsWithItsRole()
    {
        var messages = Handlers().Select(handler => handler.Message).Where(message => !message.IsGenericType).Distinct().ToList();

        var misnamed = messages.Where(message => !NamesItsRole(message)).Select(message => message.FullName);

        messages.ShouldNotBeEmpty();
        misnamed.ShouldBeEmpty();
    }

    // N8, rule 6 and W12: every message has a fixed name on the wire, in kebab case, so a moved or renamed type keeps it. No two
    // messages share a name, or one would arrive at the other's handler.
    [Fact]
    public void NameMessages_EveryHandledMessage_HasAFixedNameOfItsOwn()
    {
        var messages = Handlers().Select(handler => handler.Message).Where(message => !message.IsGenericType).Distinct().ToList();
        var names = messages.Select(message => message.GetCustomAttribute<SharedKernel.MessageNameAttribute>()?.Name).ToList();

        var unnamed = messages.Where(message => message.GetCustomAttribute<SharedKernel.MessageNameAttribute>() is null).Select(message => message.FullName);
        var misnamed = names.OfType<string>().Where(name => !KebabCase().IsMatch(name));
        var shared = names.OfType<string>().GroupBy(name => name).Where(name => name.Count() > 1).Select(name => name.Key);

        messages.ShouldNotBeEmpty();
        unnamed.ShouldBeEmpty();
        misnamed.ShouldBeEmpty();
        shared.ShouldBeEmpty();
    }

    // N8, rule 2: the handler of a command or a query is named after it: InviteMemberCommand has InviteMemberHandler.
    [Fact]
    public void NameHandlers_HandlerOfACommandOrQuery_IsNamedAfterIt()
    {
        var handlers = Handlers().Where(handler => EndsWithAny(handler.Message.Name, "Command", "Query")).ToList();

        var misnamed = handlers
            .Where(handler => handler.Type.Name != $"{ActionOf(handler.Message)}Handler")
            .Select(handler => $"{handler.Type.FullName}: {handler.Message.Name}");

        handlers.ShouldNotBeEmpty();
        misnamed.ShouldBeEmpty();
    }

    // N8, rule 2: the validator of a command is named after it: InviteMemberCommand has InviteMemberValidator.
    [Fact]
    public void NameValidators_EveryValidator_IsNamedAfterItsCommand()
    {
        var validators = Validators().ToList();

        var misnamed = validators
            .Where(validator => validator.Name != $"{ActionOf(validator.BaseType!.GetGenericArguments()[0])}Validator")
            .Select(validator => validator.FullName);

        validators.ShouldNotBeEmpty();
        misnamed.ShouldBeEmpty();
    }

    // N8, rule 3: a use case has a folder of its own in its resource folder, {Resource}/{Action}/, with its command or query, handler and
    // validator. A handler of an event names its action: RecordMemberInvitedHandler sits in Members/RecordMemberInvited/.
    [Fact]
    public void PlaceUseCases_EveryCommandQueryHandlerAndValidator_SitsInItsUseCaseFolder()
    {
        var useCaseTypes = UseCaseTypes().ToList();

        var misplaced = useCaseTypes
            .Where(useCase => !SitsInItsUseCaseFolder(useCase.Type, useCase.Action))
            .Select(useCase => useCase.Type.FullName);

        useCaseTypes.ShouldNotBeEmpty();
        misplaced.ShouldBeEmpty();
    }

    // N8, the exception of rule 3b: a project's root holds only what the whole module shares: the error lists, the permission list, the
    // list of audit operations and the module's registration file. The DbContext sits at the root of Persistence (rule 5). Every other file sits in the folder of its resource or of what it is.
    [Fact]
    public void PlaceFiles_ModuleProjectRoot_HoldsOnlyWhatTheModuleShares()
    {
        var rootFiles = Solution.ModuleProjects
            .SelectMany(project => Directory.EnumerateFiles(Solution.FolderOf(project), "*.cs").Select(file => (Project: project, File: Path.GetFileName(file))))
            .ToList();

        var misplaced = rootFiles
            .Where(file => !SharedByTheModule().IsMatch(file.File))
            .Select(file => $"{file.Project}: {file.File}");

        rootFiles.ShouldNotBeEmpty();
        misplaced.ShouldBeEmpty();
    }

    // N8, rule 5: every project of a module uses the resource folders of its Application and Domain, so the folder is the topic and the
    // suffix the role: Contracts at its root, Api below each audience folder, Infrastructure below Persistence.
    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void PlaceFolders_ContractsApiAndInfrastructure_UseTheResourceFolders(string module)
    {
        var resources = ResourceFoldersOf(module);

        var unknown = FoldersByResourceOf(module).Where(folder => !resources.Contains(folder)).Distinct();

        resources.ShouldNotBeEmpty();
        unknown.ShouldBeEmpty();
    }

    private static IEnumerable<Type> PermissionLists() =>
        Solution.LayerOfEveryModule("Contracts")
            .SelectMany(project => Solution.Load(project).GetTypes())
            .Where(type => type.Name == "Permissions");

    // Every folder under the project's own folder, without the build output.
    private static IEnumerable<string> FoldersOf(string project)
    {
        var root = Solution.FolderOf(project);
        return Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Where(folder => !Path.GetRelativePath(root, folder).Split(Path.DirectorySeparatorChar).Any(BuildOutput.Contains));
    }

    // The top-level types the project declares and the ones it takes from the projects it references directly.
    private static HashSet<string> TypeNamesSeenBy(string project) =>
    [
        .. Solution.ReadProjectFile(project).ProjectReferences.Append(project)
            .SelectMany(seen => Solution.Load(seen).GetTypes())
            .Where(type => !type.IsNested && !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
            .Select(type => type.Name),
    ];

    // The handlers that are no saga, each with its action, the commands and queries they handle in their own project, and the validators.
    private static IEnumerable<(Type Type, string Action)> UseCaseTypes() =>
        Handlers().Where(handler => !handler.Type.IsSubclassOf(typeof(Wolverine.Saga)))
            .SelectMany(handler => UseCaseTypesOf(handler.Type, handler.Message))
            .Concat(Validators().Select(validator => (Type: validator, Action: validator.Name[..^"Validator".Length])))
            .Distinct();

    private static IEnumerable<(Type Type, string Action)> UseCaseTypesOf(Type handler, Type message)
    {
        yield return (handler, handler.Name[..^"Handler".Length]);
        if (EndsWithAny(message.Name, "Command", "Query") && message.Assembly == handler.Assembly)
        {
            yield return (message, ActionOf(message));
        }
    }

    // {Project}.{Resource}.{Action}: the namespace follows the folder (IDE0130).
    private static bool SitsInItsUseCaseFolder(Type type, string action) =>
        type.Namespace is { } name
        && name.Split('.') is [.., var resource, var folder]
        && folder == action
        && name == $"{type.Assembly.GetName().Name}.{resource}.{folder}";

    // The resource folders of a module: the first folders of its Application and Domain projects.
    private static HashSet<string> ResourceFoldersOf(string module) =>
        [.. ResourceLayers.SelectMany(layer => FirstFoldersOf(Solution.FolderOf($"{module}.{layer}")))];

    // The folders that name a resource in the other projects: Contracts at its root, Api below each audience folder, Infrastructure
    // below Persistence, without the pieces Persistence holds for itself.
    private static IEnumerable<string> FoldersByResourceOf(string module)
    {
        var api = Solution.FolderOf($"{module}.Api");
        var persistence = Path.Combine(Solution.FolderOf($"{module}.Infrastructure"), "Persistence");
        return FirstFoldersOf(Solution.FolderOf($"{module}.Contracts"))
            .Concat(Audiences.SelectMany(audience => FirstFoldersOf(Path.Combine(api, audience))))
            .Concat(FirstFoldersOf(persistence).Except(PersistencePieces));
    }

    // The C# files of every module project but its tests, without the migrations EF Core writes.
    private static IEnumerable<string> ModuleSourceFiles() =>
        Solution.ModuleProjects.SelectMany(Solution.SourceFilesOf)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    // Each handler of a module's Application and the message it handles: the first parameter of each Handle or HandleAsync method, as
    // Wolverine finds them (W2). A saga handles its messages too.
    private static IEnumerable<(Type Type, Type Message)> Handlers() =>
        Solution.LayerOfEveryModule("Application")
            .SelectMany(project => Solution.Load(project).GetExportedTypes())
            .Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal) || type.IsSubclassOf(typeof(Wolverine.Saga)))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.Name is "Handle" or "HandleAsync" && method.GetParameters().Length > 0)
                .Select(method => (Type: type, Message: method.GetParameters()[0].ParameterType)));

    private static IEnumerable<Type> Validators() =>
        Solution.LayerOfEveryModule("Application")
            .SelectMany(project => Solution.Load(project).GetExportedTypes())
            .Where(type => type is { IsAbstract: false, BaseType.IsGenericType: true } && type.BaseType.GetGenericTypeDefinition() == typeof(FluentValidation.AbstractValidator<>));

    // The action of a command or a query: its name without the role.
    private static string ActionOf(Type message) =>
        message.Name.EndsWith("Command", StringComparison.Ordinal) ? message.Name[..^"Command".Length]
        : message.Name.EndsWith("Query", StringComparison.Ordinal) ? message.Name[..^"Query".Length]
        : message.Name;

    private static bool NamesItsRole(Type message) =>
        message.Assembly.GetName().Name!.EndsWith(".Contracts", StringComparison.Ordinal)
            ? EndsWithAny(message.Name, "Event", "Reply")
            : EndsWithAny(message.Name, "Command", "Query", "Reply", "TimedOut");

    private static bool EndsWithAny(string name, params string[] suffixes) => suffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal));

    private static IEnumerable<string> FirstFoldersOf(string folder) =>
        Directory.Exists(folder)
            ? Directory.EnumerateDirectories(folder).Select(Path.GetFileName).OfType<string>().Where(name => !BuildOutput.Contains(name))
            : [];

    // The types a unit test project tests: a module's projects for a module's tests, or the one project it is named after.
    private static IEnumerable<Type> TestedTypesOf(string project)
    {
        var tested = project[..^".UnitTests".Length];
        var projects = Solution.Modules.Contains(tested) ? Solution.ProjectsOf(tested) : [tested];
        return projects.SelectMany(seen => Solution.Load(seen).GetTypes()).Where(type => !type.IsNested);
    }

    // Every top-level class that holds a test.
    private static IEnumerable<Type> TestClassesOf(string project) => TestMethodsOf(project).Select(test => test.DeclaringType!).Where(type => !type.IsNested).Distinct();

    // Every method xunit runs as a test: [Fact], and [Theory], which derives from it.
    private static IEnumerable<MethodInfo> TestMethodsOf(string project) =>
        Solution.Load(project).GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.IsDefined(typeof(FactAttribute), inherit: true));

    // The work a test names first: Operation in Operation_Scenario_ExpectedOutcome (section 9).
    private static string UseCaseOf(MethodInfo test) => test.Name.Split('_')[0];

    // The folder of a type below its project, as its namespace says (IDE0130): "" at the root, ".Tenants" in Tenants/.
    private static string FolderOf(Type type) => type.Namespace![type.Assembly.GetName().Name!.Length..];

    // Each operation of the OpenAPI document, and the audience and resource folder of the endpoints class that declares it (N7d, N11c).
    private static Dictionary<string, string> EndpointFolders()
    {
        var operations = JsonDocument.Parse(File.ReadAllText(Path.Combine(Solution.Root, "openapi/v1.json"))).RootElement
            .GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Select(operation => operation.Value.GetProperty("operationId").GetString()!)
            .ToHashSet();
        return Solution.LayerOfEveryModule("Api")
            .SelectMany(project => Solution.Load(project).GetTypes())
            .Where(type => type.Name.EndsWith("Endpoints", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => operations.Contains(method.Name))
                .Select(method => (Operation: method.Name, Folder: FolderOf(type)[1..])))
            .ToDictionary(endpoint => endpoint.Operation, endpoint => endpoint.Folder);
    }

    private static bool IsProjectDotKindTests(string project)
    {
        var match = ProjectDotKindTests().Match(project);
        var tested = match.Groups["project"].Value;

        return match.Success && (Solution.ProductionProjects.Contains(tested) || Solution.Modules.Contains(tested));
    }

    private static string SnakeCase(string name) => WordStart().Replace(name, "_$1").ToLowerInvariant();

    [GeneratedRegex(@"^(platform|tenant)\.[a-z]+(_[a-z]+)*\.[a-z]+(_[a-z]+)*$")]
    private static partial Regex AudienceResourceAction();

    [GeneratedRegex(@"^(?<project>.+)\.(Unit|Integration|EndToEnd|Architecture)Tests$")]
    private static partial Regex ProjectDotKindTests();

    [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
    private static partial Regex WordStart();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KebabCase();

    [GeneratedRegex(@"^(public|internal)( (sealed|static|abstract|partial))* (class|record|struct|interface|enum) (?<name>\w+)", RegexOptions.Multiline)]
    private static partial Regex TopLevelType();

    [GeneratedRegex(@"^(Errors|\w+ErrorCodes|PermissionPools|Permissions\.g|AuditOperations|\w+Module|\w+Infrastructure)\.cs$")]
    private static partial Regex SharedByTheModule();
}
