using SharedKernel;

namespace Notifications.Application.EmailPreferences.GetEmailPreferences;

/// <summary>The user's email preferences, of the categories that can reach them.</summary>
[MessageName("get-email-preferences")]
public sealed record GetEmailPreferencesQuery(Guid UserId, IReadOnlyList<string> Categories);
