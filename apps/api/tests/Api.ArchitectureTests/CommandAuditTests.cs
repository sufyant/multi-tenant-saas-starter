using System.Reflection;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Application.Users.UserDeletion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Runtime.Handlers;
using Solution = Api.ArchitectureTests.Fixtures.Solution;

namespace Api.ArchitectureTests;

// K3: every command publishes, through the outbox, an event that Audit handles. A command is a message that an endpoint sends: a message type with a handler that a module's Api or Infrastructure project creates, other than a query (List..., Get...).
// A handler publishes the messages it returns, so the test follows the types each handler returns, through the handlers of those
// types (the first step of a saga, for one), until it reaches a message that an Audit handler handles. Read from Wolverine's own
// handler graph of the application composed as a worker, and from the module sources.
public sealed class CommandAuditTests : IAsyncLifetime
{
    private const string Audit = "Audit.Application";

    private readonly ComposedApplication _application = new("worker");

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _application.DisposeAsync();

    [Fact]
    public void ReadTheHandlerGraph_EveryCommand_PublishesAnEventThatAuditHandles()
    {
        var handlers = HandlersByMessage();
        var commands = CommandsOf(handlers);

        var unaudited = commands.Where(command => !ReachesAudit(command, handlers)).Select(command => command.Name);

        commands.ShouldContain(typeof(CreateTenantCommand));
        commands.ShouldContain(typeof(DeleteUserCommand));
        unaudited.ShouldBeEmpty("A command publishes, through the outbox, one event that Audit handles (K3).");
    }

    // The handler methods of each message type: its chain's own and those Wolverine separated onto endpoints of their own (W4).
    private Dictionary<Type, MethodInfo[]> HandlersByMessage()
    {
        var graph = _application.Services.GetRequiredService<HandlerGraph>();
        return graph.Chains
            .Concat(graph.Chains.SelectMany(chain => chain.ByEndpoint))
            .GroupBy(chain => chain.MessageType)
            .ToDictionary(message => message.Key, message => message.SelectMany(chain => chain.Handlers).Select(call => call.Method).Distinct().ToArray());
    }

    private static List<Type> CommandsOf(Dictionary<Type, MethodInfo[]> handlers)
    {
        var created = Solution.Modules
            .SelectMany(module => new[] { $"{module}.Api", $"{module}.Infrastructure" })
            .SelectMany(Solution.SourceFilesOf)
            .SelectMany(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            .Select(creation => creation.Type is QualifiedNameSyntax qualified ? qualified.Right.Identifier.Text : creation.Type.ToString())
            .ToHashSet(StringComparer.Ordinal);

        return [.. handlers.Keys.Where(message => created.Contains(message.Name) && !IsQuery(message))];
    }

    private static bool IsQuery(Type message) =>
        message.Name.StartsWith("List", StringComparison.Ordinal) || message.Name.StartsWith("Get", StringComparison.Ordinal);

    private static bool ReachesAudit(Type command, Dictionary<Type, MethodInfo[]> handlers)
    {
        var seen = new HashSet<Type> { command };
        var waiting = new Queue<Type>([command]);
        while (waiting.TryDequeue(out var message))
        {
            var published = handlers[message].SelectMany(method => TypesIn(method.ReturnType)).Where(handlers.ContainsKey).ToList();
            if (published.Any(type => handlers[type].Any(method => method.DeclaringType!.Assembly.GetName().Name == Audit)))
            {
                return true;
            }

            published.Where(seen.Add).ToList().ForEach(waiting.Enqueue);
        }

        return false;
    }

    // A type and every type argument in it: Task<(Result<T>, TenantSuspendedEvent?)> gives TenantSuspendedEvent, among others.
    private static IEnumerable<Type> TypesIn(Type type) =>
        type.IsGenericType ? [type, .. type.GetGenericArguments().SelectMany(TypesIn)] : [type];
}
