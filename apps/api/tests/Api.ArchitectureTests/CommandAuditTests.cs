using System.Reflection;
using ControlPlane.Application.Tenants.CreateTenant;
using ControlPlane.Application.Users.DeleteUser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Runtime.Handlers;
using Solution = Api.ArchitectureTests.Fixtures.Solution;

namespace Api.ArchitectureTests;

// K3: every command publishes, through the outbox, an event that Audit handles. A command is a message that an endpoint sends: a message type with a handler that a module's Api or Infrastructure project creates, other than a query (List..., Get...).
// A handler publishes the messages it returns, so the test follows the types each handler returns, through the handlers of those
// types (the first step of a saga, for one), until it reaches a message that an Audit handler handles. A handler that returns
// OutgoingMessages adds its messages to that list in its source: the test reads what it adds there, and nothing it only builds. Read from Wolverine's own
// handler graph of the application composed as a worker, and from the module sources.
public sealed class CommandAuditTests : IAsyncLifetime
{
    private const string Audit = "Audit.Application";

    // K3's exception: a change of a user's read state of their notifications is their own view, not a change of the product's data (U7).
    private static readonly string[] ReadStateCommands = ["MarkNotificationReadCommand", "MarkAllNotificationsReadCommand"];

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
        message.Name.StartsWith("List", StringComparison.Ordinal) || message.Name.StartsWith("Get", StringComparison.Ordinal) || ReadStateCommands.Contains(message.Name);

    private static bool ReachesAudit(Type command, Dictionary<Type, MethodInfo[]> handlers)
    {
        var seen = new HashSet<Type> { command };
        var waiting = new Queue<Type>([command]);
        while (waiting.TryDequeue(out var message))
        {
            var published = handlers[message].SelectMany(method => PublishedBy(method, handlers)).Where(handlers.ContainsKey).ToList();
            if (published.Any(type => handlers[type].Any(method => method.DeclaringType!.Assembly.GetName().Name == Audit)))
            {
                return true;
            }

            published.Where(seen.Add).ToList().ForEach(waiting.Enqueue);
        }

        return false;
    }

    // The types a handler returns and, when it returns OutgoingMessages, the message types its source adds to the messages it returns:
    // an element of a list it returns or declares as OutgoingMessages ([new X(...), .. X.For(...), x]), or an argument of Add or Insert on
    // such a list. A type the handler builds and never adds is not published.
    private static List<Type> PublishedBy(MethodInfo method, Dictionary<Type, MethodInfo[]> handlers)
    {
        var returned = TypesIn(method.ReturnType).ToList();
        if (!returned.Contains(typeof(OutgoingMessages)))
        {
            return returned;
        }

        var added = AddedInSourceOf(method).ToHashSet(StringComparer.Ordinal);
        return [.. returned, .. handlers.Keys.Where(message => !message.IsGenericType && added.Contains(message.Name))];
    }

    private static IEnumerable<string> AddedInSourceOf(MethodInfo method)
    {
        var file = Solution.SourceFilesOf(method.DeclaringType!.Assembly.GetName().Name!)
            .Single(path => Path.GetFileNameWithoutExtension(path) == method.DeclaringType.Name);
        return CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(declaration => declaration.Identifier.Text == method.Name)
            .SelectMany(AddedIn);
    }

    private static IEnumerable<string> AddedIn(MethodDeclarationSyntax method)
    {
        var nodes = method.DescendantNodes().ToList();
        var types = DeclaredTypes(nodes);
        var lists = types.Where(local => local.Value == nameof(OutgoingMessages)).Select(local => local.Key).ToHashSet(StringComparer.Ordinal);

        IEnumerable<ExpressionSyntax> returned = nodes.OfType<ReturnStatementSyntax>().Select(statement => statement.Expression).OfType<ExpressionSyntax>()
            .Concat(nodes.OfType<ArrowExpressionClauseSyntax>().Select(arrow => arrow.Expression))
            .SelectMany(Branches)
            .SelectMany(expression => expression is TupleExpressionSyntax tuple ? tuple.Arguments.Select(argument => argument.Expression).SelectMany(Branches) : [expression]);
        var declared = nodes.OfType<VariableDeclaratorSyntax>()
            .Where(declarator => lists.Contains(declarator.Identifier.Text))
            .Select(declarator => declarator.Initializer?.Value)
            .OfType<ExpressionSyntax>()
            .SelectMany(Branches);
        var elements = returned.Concat(declared).OfType<CollectionExpressionSyntax>()
            .SelectMany(collection => collection.Elements)
            .Select(element => element switch
            {
                ExpressionElementSyntax expression => expression.Expression,
                SpreadElementSyntax spread => spread.Expression,
                _ => null,
            })
            .OfType<ExpressionSyntax>();
        var arguments = nodes.OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax
            {
                Name.Identifier.Text: "Add" or "Insert",
                Expression: IdentifierNameSyntax list,
            } && lists.Contains(list.Identifier.Text))
            .Select(invocation => invocation.ArgumentList.Arguments[^1].Expression);

        return elements.Concat(arguments).SelectMany(Branches).Select(expression => TypeOf(expression, types)).OfType<string>();
    }

    // The type each local of the method is declared with: X x = ..., (X x, Y y) = ..., or an out or pattern declaration.
    private static Dictionary<string, string> DeclaredTypes(List<SyntaxNode> nodes) =>
        nodes.OfType<VariableDeclarationSyntax>()
            .SelectMany(declaration => declaration.Variables.Select(variable => (Name: variable.Identifier.Text, Type: NameOf(declaration.Type))))
            .Concat(nodes.OfType<DeclarationExpressionSyntax>()
                .Where(declaration => declaration.Designation is SingleVariableDesignationSyntax)
                .Select(declaration => (Name: ((SingleVariableDesignationSyntax)declaration.Designation).Identifier.Text, Type: NameOf(declaration.Type))))
            .GroupBy(local => local.Name, StringComparer.Ordinal)
            .ToDictionary(local => local.Key, local => local.First().Type, StringComparer.Ordinal);

    // A conditional gives both branches; parentheses give what they hold.
    private static IEnumerable<ExpressionSyntax> Branches(ExpressionSyntax expression) => expression switch
    {
        ConditionalExpressionSyntax conditional => [.. Branches(conditional.WhenTrue), .. Branches(conditional.WhenFalse)],
        ParenthesizedExpressionSyntax parenthesized => Branches(parenthesized.Expression),
        _ => [expression],
    };

    // The type of an added message: new X(...); X.For(...) and x.WithDeliveryOptions(...) by their receiver; a local by its declaration.
    private static string? TypeOf(ExpressionSyntax expression, Dictionary<string, string> locals) => expression switch
    {
        ObjectCreationExpressionSyntax creation => NameOf(creation.Type),
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } => TypeOf(access.Expression, locals),
        PostfixUnaryExpressionSyntax postfix => TypeOf(postfix.Operand, locals),
        IdentifierNameSyntax name => locals.GetValueOrDefault(name.Identifier.Text, name.Identifier.Text),
        _ => null,
    };

    private static string NameOf(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => NameOf(qualified.Right),
        NullableTypeSyntax nullable => NameOf(nullable.ElementType),
        SimpleNameSyntax simple => simple.Identifier.Text,
        _ => type.ToString(),
    };

    // A type and every type argument in it: Task<(Result<T>, TenantSuspendedEvent?)> gives TenantSuspendedEvent, among others.
    private static IEnumerable<Type> TypesIn(Type type) =>
        type.IsGenericType ? [type, .. type.GetGenericArguments().SelectMany(TypesIn)] : [type];

    // The handler methods of each message type: its chain's own and those Wolverine separated onto endpoints of their own (W4).
    private Dictionary<Type, MethodInfo[]> HandlersByMessage()
    {
        var graph = _application.Services.GetRequiredService<HandlerGraph>();
        return graph.Chains
            .Concat(graph.Chains.SelectMany(chain => chain.ByEndpoint))
            .GroupBy(chain => chain.MessageType)
            .ToDictionary(message => message.Key, message => message.SelectMany(chain => chain.Handlers).Select(call => call.Method).Distinct().ToArray());
    }
}
