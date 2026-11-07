using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Fixtures;

// The hosts on one database are nodes of one Wolverine cluster. A node answers a command of another node, such as the leader's agent
// assignment, with Wolverine's acknowledgement at any moment, and a tracked session always records it (section 9, rule 10).
internal static class TrackedMessages
{
    // The messages the application sent, without Wolverine's own.
    public static IEnumerable<object> OwnMessagesSent(this ITrackedSession session) =>
        session.Sent.AllMessages().Where(message => message.GetType().Assembly != typeof(Envelope).Assembly);
}
