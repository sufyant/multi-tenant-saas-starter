using ControlPlane.Application.Tenants.TenantCreation;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using Wolverine;
using Wolverine.Runtime.Handlers;

namespace Api.ArchitectureTests;

// S13: a saga shares no message with another handler. It hears its steps through replies and faults sent to it, not through events
// meant for other modules. Read from Wolverine's own handler graph of the application composed as a worker: the handlers of a
// message type are its chain's own and those Wolverine separated onto endpoints of their own (W4).
public sealed class SagaMessageTests : IAsyncLifetime
{
    private const string WhyNot =
        "Wolverine runs a saga alone at the endpoint its message arrives on, so another handler of that message would never run in "
        + "the roles web and worker (W5). Send the saga a reply of its own (S13).";

    private readonly ComposedApplication _application = new("worker");

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _application.DisposeAsync();

    [Fact]
    public void ReadTheHandlerGraph_EveryMessageASagaHandles_HasNoOtherHandler()
    {
        var sagaMessages = SagaMessages();

        var shared = sagaMessages
            .Where(message => message.Value.Length > 1)
            .Select(message => $"{message.Key.Name} -> {string.Join(", ", message.Value.Select(handler => handler.Name))}");

        sagaMessages.Select(message => message.Key).ShouldContain(typeof(SendInvitationEmailReply));
        sagaMessages.Select(message => message.Key).ShouldContain(typeof(Fault<ActivateTenant>));
        shared.ShouldBeEmpty(WhyNot);
    }

    // N3: a saga hears only the replies to its steps, its own timeouts and Wolverine's faults; never a public event.
    [Fact]
    public void ReadTheHandlerGraph_EveryMessageASagaHandles_IsAReplyATimeoutOrAFault()
    {
        var others = SagaMessages()
            .Select(message => message.Key)
            .Where(message => !IsReply(message) && !IsTimeout(message) && !IsFault(message))
            .Select(message => message.FullName);

        others.ShouldBeEmpty("A saga handles only ...Reply, ...TimedOut and Fault<T> (N3).");
    }

    // N3: a reply answers a saga's step, so no other handler hears it. The name makes S13 visible.
    [Fact]
    public void ReadTheHandlerGraph_EveryReply_IsHandledOnlyByASaga()
    {
        var replies = HandlersByMessage().Where(message => IsReply(message.Key)).ToList();

        var heardElsewhere = replies
            .Where(message => message.Value.Any(handler => !IsSaga(handler)))
            .Select(message => $"{message.Key.Name} -> {string.Join(", ", message.Value.Select(handler => handler.Name))}");

        replies.ShouldNotBeEmpty();
        heardElsewhere.ShouldBeEmpty("Only a saga handles a ...Reply (N3).");
    }

    private Dictionary<Type, Type[]> HandlersByMessage()
    {
        var graph = _application.Services.GetRequiredService<HandlerGraph>();
        return graph.Chains
            .Concat(graph.Chains.SelectMany(chain => chain.ByEndpoint))
            .GroupBy(chain => chain.MessageType)
            .ToDictionary(message => message.Key, message => message.SelectMany(chain => chain.Handlers).Select(call => call.HandlerType).Distinct().ToArray());
    }

    private List<KeyValuePair<Type, Type[]>> SagaMessages() => [.. HandlersByMessage().Where(message => message.Value.Any(IsSaga))];

    private static bool IsReply(Type message) => message.Name.EndsWith("Reply", StringComparison.Ordinal);

    private static bool IsTimeout(Type message) => message.Name.EndsWith("TimedOut", StringComparison.Ordinal);

    private static bool IsFault(Type message) => message.IsGenericType && message.GetGenericTypeDefinition() == typeof(Fault<>);

    private static bool IsSaga(Type handler) => handler.IsAssignableTo(typeof(Saga));
}
