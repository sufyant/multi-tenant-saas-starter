using SharedKernel;

namespace Notifications.Application.EmailPreferences.ChangeEmailPreferences;

/// <summary>
/// The user's new email preferences (U5): the main switch, and a choice for some categories. Only a category that can reach the user is
/// kept, so the endpoint passes them.
/// </summary>
[MessageName("change-email-preferences")]
public sealed record ChangeEmailPreferencesCommand(
    Guid UserId,
    bool Enabled,
    IReadOnlyDictionary<string, bool> Categories,
    IReadOnlyList<string> Reachable);
