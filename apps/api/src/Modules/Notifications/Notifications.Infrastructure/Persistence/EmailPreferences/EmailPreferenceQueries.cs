using Microsoft.EntityFrameworkCore;
using Notifications.Application.EmailPreferences;
using Notifications.Domain.EmailPreferences;

namespace Notifications.Infrastructure.Persistence.EmailPreferences;

/// <summary>The reads of the email preferences on the module's DbContext, without tracking (Q2).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class EmailPreferenceQueries(NotificationsDbContext notifications) : IEmailPreferenceQueries
{
    Task IEmailPreferenceQueries.DeclareUserAsync(Guid userId, CancellationToken cancellationToken) =>
        notifications.DeclareRecipientAsync(userId, cancellationToken);

    async Task<EmailPreferencesView> IEmailPreferenceQueries.FindAsync(Guid userId, IReadOnlyList<string> categories, CancellationToken cancellationToken)
    {
        var preference = await notifications.EmailPreferences.AsNoTracking().SingleOrDefaultAsync(found => found.UserId == userId, cancellationToken)
            ?? EmailPreference.DefaultFor(userId);
        return new(preference.Enabled, [.. categories.Select(category => new CategoryEmailPreferenceView(category, preference.IsOn(category)))]);
    }
}
