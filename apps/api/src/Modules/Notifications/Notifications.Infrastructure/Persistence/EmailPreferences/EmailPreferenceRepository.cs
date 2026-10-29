using Microsoft.EntityFrameworkCore;
using Notifications.Application.EmailPreferences;
using Notifications.Domain.EmailPreferences;

namespace Notifications.Infrastructure.Persistence.EmailPreferences;

/// <summary>The email preferences on the module's DbContext, in the transaction Wolverine began for the handler.</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class EmailPreferenceRepository(NotificationsDbContext notifications) : IEmailPreferenceRepository
{
    Task IEmailPreferenceRepository.DeclareUserAsync(Guid userId, CancellationToken cancellationToken) =>
        notifications.DeclareRecipientAsync(userId, cancellationToken);

    async Task<EmailPreference> IEmailPreferenceRepository.FindEmailPreferenceForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        await notifications.EmailPreferences
            .FromSql($"SELECT * FROM notifications.email_preferences WHERE user_id = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
        ?? EmailPreference.DefaultFor(userId);

    async Task IEmailPreferenceRepository.SaveAsync(EmailPreference preference, CancellationToken cancellationToken)
    {
        var entry = notifications.Entry(preference);
        if (preference.IsDefault)
        {
            if (entry.State != EntityState.Detached)
            {
                notifications.EmailPreferences.Remove(preference);
            }
        }
        else if (entry.State == EntityState.Detached)
        {
            notifications.EmailPreferences.Add(preference);
        }

        await notifications.SaveChangesAsync(cancellationToken);
    }

    Task IEmailPreferenceRepository.DeleteAsync(CancellationToken cancellationToken) =>
        notifications.EmailPreferences.ExecuteDeleteAsync(cancellationToken);
}
