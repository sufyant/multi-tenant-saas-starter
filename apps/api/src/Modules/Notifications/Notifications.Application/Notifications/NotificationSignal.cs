using System.Text.Json;
using System.Text.Json.Serialization;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Notifications.Application.Notifications;

/// <summary>
/// The live signal that a user's notifications changed (U6): only the bell that changed, a tenant's (with its id), the personal one or the
/// platform's. It carries no content, so an open stream leaks nothing, even after its token expired: the client reads the list and the count
/// through the endpoints, which authorize the read. A personal change concerns every tenant bell.
/// </summary>
public static class NotificationSignal
{
    /// <summary>The name of the live event.</summary>
    public const string EventName = "notifications";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static LiveEvent Of(Inbox inbox) =>
        new(EventName, JsonSerializer.Serialize(new Signal(JsonEnum<NotificationBell>.NameOf(inbox.Bell), inbox.TenantId), Json));

    private sealed record Signal(string Scope, Guid? TenantId);
}
