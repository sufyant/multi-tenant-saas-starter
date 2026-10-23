using System.Reflection;
using SharedKernel;
using Wolverine.Util;

namespace Api.Messaging;

// W12: Wolverine sends and stores each message under its message type name. A message that carries MessageNameAttribute gets that
// name, so a moved or renamed type keeps its name on the wire; any other type keeps Wolverine's own name. Wolverine keeps its naming
// strategies for the whole process, and InsertFirst adds this one once and clears the names it has resolved before.
//
// Wolverine's list of naming strategies is a static List without a lock, and every new WolverineOptions reads it. A host registers
// this strategy before it creates its WolverineOptions, under a lock, so the one change to the list ends before any host in the
// process reads it (W14).
internal sealed class MessageNames : IMessageTypeNaming
{
    private static readonly Lock Registration = new();

    public static void Register()
    {
        lock (Registration)
        {
            WolverineMessageNaming.InsertFirst<MessageNames>();
        }
    }

    public bool TryDetermineName(Type messageType, out string messageTypeName)
    {
        messageTypeName = messageType.GetCustomAttribute<MessageNameAttribute>()?.Name!;
        return messageTypeName is not null;
    }
}
