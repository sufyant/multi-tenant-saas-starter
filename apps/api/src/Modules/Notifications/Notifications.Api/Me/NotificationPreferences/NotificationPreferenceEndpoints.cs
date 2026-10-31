using JasperFx;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Notifications.Api.Statuses;
using Notifications.Api.Tenant.Notifications;
using Notifications.Application;
using Notifications.Application.NotificationPreferences;
using Notifications.Application.NotificationPreferences.ChangeNotificationPreferences;
using Notifications.Application.NotificationPreferences.GetNotificationPreferences;
using Notifications.Application.Notifications.GetNotificationPermissions;
using Notifications.Application.Notifications.ListNotificationTenants;
using SharedKernel;
using Wolverine;

namespace Notifications.Api.Me.NotificationPreferences;

/// <summary>
/// The signed-in user's notification preferences (U5), the same in every tenant. They list only the categories that can reach the user,
/// from the user's permissions in each of their tenants, each read in that tenant's transaction (R4), and whether they are a system
/// admin. The answer names each category's types in the reader's language.
/// </summary>
public static class NotificationPreferenceEndpoints
{
    public static void Map(RouteGroupBuilder me)
    {
        me.MapGet("/notification-preferences", GetNotificationPreferences);
        me.MapPut("/notification-preferences", ChangeNotificationPreferences);
    }

    private static async Task<Result<NotificationPreferencesResponse>> GetNotificationPreferences(HttpContext http, IMessageBus bus, CancellationToken cancellationToken)
    {
        var reader = await NotificationEndpoints.ReaderAsync(http, bus, cancellationToken);
        if (reader.UserId is not { } userId)
        {
            return Errors.NotificationPreferencesNotFound;
        }

        var reachable = await ReachableAsync(userId, reader.IsSystemAdmin, bus, cancellationToken);
        var preferences = await bus.InvokeForTenantAsync<NotificationPreferencesView>(
            StorageConstants.DefaultTenantId, new GetNotificationPreferencesQuery(userId, reachable, reader.Language), cancellationToken);
        return ToResponse(preferences);
    }

    private static async Task<Result<NotificationPreferencesResponse>> ChangeNotificationPreferences(
        ChangeNotificationPreferencesRequest request, HttpContext http, IMessageBus bus, CancellationToken cancellationToken)
    {
        var reader = await NotificationEndpoints.ReaderAsync(http, bus, cancellationToken);
        if (reader.UserId is not { } userId)
        {
            return Errors.NotificationPreferencesNotFound;
        }

        var reachable = await ReachableAsync(userId, reader.IsSystemAdmin, bus, cancellationToken);
        var changed = await bus.InvokeForTenantAsync<Result<NotificationPreferencesView>>(
            StorageConstants.DefaultTenantId,
            new ChangeNotificationPreferencesCommand(
                userId,
                request.Channels.DistinctBy(choice => choice.Channel).ToDictionary(choice => NameOf(choice.Channel), choice => choice.Enabled, StringComparer.Ordinal),
                [
                    .. request.Categories
                        .DistinctBy(choice => (choice.Category, choice.Channel))
                        .Select(choice => new CategoryChoice(choice.Category, NameOf(choice.Channel), choice.Enabled)),
                ],
                reachable,
                reader.Language),
            cancellationToken);
        return changed.Map(ToResponse);
    }

    // The user's permissions in each tenant, one tenant's transaction at a time, and whether the platform's door is theirs.
    private static async Task<IReadOnlyList<string>> ReachableAsync(Guid userId, bool isSystemAdmin, IMessageBus bus, CancellationToken cancellationToken)
    {
        var tenants = await bus.InvokeForTenantAsync<IReadOnlyList<Guid>>(StorageConstants.DefaultTenantId, new ListNotificationTenantsQuery(userId), cancellationToken);
        List<IReadOnlySet<string>> permissions = [];
        foreach (var tenantId in tenants)
        {
            permissions.Add(await bus.InvokeForTenantAsync<IReadOnlySet<string>>(tenantId.ToString(), new GetNotificationPermissionsQuery(userId), cancellationToken));
        }

        return NotificationReach.CategoriesOf(permissions, isSystemAdmin);
    }

    // The name of the catalog's channel, as the command carries it.
    private static string NameOf(NotificationChannelKind channel) => channel switch
    {
        NotificationChannelKind.Web => "Web",
        NotificationChannelKind.Email => "Email",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "A channel of the preferences has no channel of the catalog."),
    };

    private static NotificationPreferencesResponse ToResponse(NotificationPreferencesView preferences) =>
        new(
            [.. preferences.Channels.Select(ToResponse)],
            [
                .. preferences.Categories.Select(category => new CategoryPreferenceResponse(
                    category.Category,
                    category.Required,
                    [.. category.Types.Select(type => new NotificationTypeNameResponse(type.Type, type.Name))],
                    [.. category.Channels.Select(ToResponse)])),
            ]);

    private static ChannelPreferenceResponse ToResponse(ChannelPreferenceView channel) => new(ResponseStatus.OfChannel(channel.Channel), channel.Enabled);
}
