using JasperFx;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Notifications.Api.Tenant.Notifications;
using Notifications.Application;
using Notifications.Application.EmailPreferences;
using Notifications.Application.EmailPreferences.ChangeEmailPreferences;
using Notifications.Application.EmailPreferences.GetEmailPreferences;
using Notifications.Application.Notifications.GetNotificationPermissions;
using Notifications.Application.Notifications.ListNotificationTenants;
using SharedKernel;
using Wolverine;

namespace Notifications.Api.Me.EmailPreferences;

/// <summary>
/// The signed-in user's email preferences for notifications (U5), the same in every tenant. They list only the categories that can reach the
/// user, from the user's permissions in each of their tenants, each read in that tenant's transaction (R4), and whether they are a system
/// admin.
/// </summary>
public static class EmailPreferenceEndpoints
{
    public static void Map(RouteGroupBuilder me)
    {
        me.MapGet("/email-preferences", GetEmailPreferences);
        me.MapPut("/email-preferences", ChangeEmailPreferences);
    }

    private static async Task<Result<EmailPreferencesResponse>> GetEmailPreferences(HttpContext http, IMessageBus bus, CancellationToken cancellationToken)
    {
        var reader = await NotificationEndpoints.ReaderAsync(http, bus, cancellationToken);
        if (reader.UserId is not { } userId)
        {
            return Errors.EmailPreferencesNotFound;
        }

        var reachable = await ReachableAsync(userId, reader.IsSystemAdmin, bus, cancellationToken);
        var preferences = await bus.InvokeForTenantAsync<EmailPreferencesView>(
            StorageConstants.DefaultTenantId, new GetEmailPreferencesQuery(userId, reachable), cancellationToken);
        return ToResponse(preferences);
    }

    private static async Task<Result<EmailPreferencesResponse>> ChangeEmailPreferences(
        ChangeEmailPreferencesRequest request, HttpContext http, IMessageBus bus, CancellationToken cancellationToken)
    {
        var reader = await NotificationEndpoints.ReaderAsync(http, bus, cancellationToken);
        if (reader.UserId is not { } userId)
        {
            return Errors.EmailPreferencesNotFound;
        }

        var reachable = await ReachableAsync(userId, reader.IsSystemAdmin, bus, cancellationToken);
        var changed = await bus.InvokeForTenantAsync<Result<EmailPreferencesView>>(
            StorageConstants.DefaultTenantId,
            new ChangeEmailPreferencesCommand(
                userId,
                request.Enabled,
                request.Categories.DistinctBy(choice => choice.Category).ToDictionary(choice => choice.Category, choice => choice.Enabled, StringComparer.Ordinal),
                reachable),
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

    private static EmailPreferencesResponse ToResponse(EmailPreferencesView preferences) =>
        new(preferences.Enabled, [.. preferences.Categories.Select(category => new CategoryEmailPreferenceResponse(category.Category, category.Enabled))]);
}
