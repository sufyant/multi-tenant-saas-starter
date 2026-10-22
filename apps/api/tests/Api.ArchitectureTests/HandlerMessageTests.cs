using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Wolverine;
using Wolverine.Runtime.Handlers;

namespace Api.ArchitectureTests;

// One way to return messages (the maintainer's decision in step r2b): a handler returns its messages in one OutgoingMessages, with its
// Result or alone, never as a tuple of messages that an early return fills with nulls. A message is a type with a fixed name on the wire
// (W12), a message wrapped with its delivery options, or OutgoingMessages. Read from Wolverine's own handler graph of the composed
// application.
public sealed class HandlerMessageTests : IAsyncLifetime
{
    private readonly ComposedApplication _application = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _application.DisposeAsync();

    [Fact]
    public void ReturnMessages_OfEachHandler_HoldAtMostOneMessageInATuple()
    {
        var graph = _application.Services.GetRequiredService<HandlerGraph>();
        var methods = graph.Chains.Concat(graph.Chains.SelectMany(chain => chain.ByEndpoint))
            .SelectMany(chain => chain.Handlers)
            .Select(call => call.Method)
            .Distinct()
            .ToList();

        var tuplesOfMessages = methods
            .Where(method => ElementsOf(Awaited(method.ReturnType)).Count(IsMessage) > 1)
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}: {method.ReturnType}");

        methods.ShouldNotBeEmpty();
        tuplesOfMessages.ShouldBeEmpty();
    }

    // Task<T> and ValueTask<T> give T.
    private static Type Awaited(Type type) =>
        type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            ? type.GetGenericArguments()[0]
            : type;

    // A tuple gives its elements; any other type is one element.
    private static Type[] ElementsOf(Type type) =>
        type.IsGenericType && type.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal)
            ? type.GetGenericArguments()
            : [type];

    private static bool IsMessage(Type type)
    {
        var element = Nullable.GetUnderlyingType(type) ?? type;
        return element == typeof(OutgoingMessages)
            || element.IsDefined(typeof(MessageNameAttribute), inherit: false)
            || (element.IsGenericType && element.GetGenericTypeDefinition() == typeof(DeliveryMessage<>));
    }
}
