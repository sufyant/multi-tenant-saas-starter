using System.Globalization;
using ControlPlane.Infrastructure;
using Microsoft.Extensions.Options;
using Notifications.Infrastructure;

namespace Api.Hosting;

// Section 6, "Tenant creation process": the saga waits for the invitation email longer than the identity provider's retries and the
// email's retries together. A shorter wait gives up on a tenant while a retry is still due. ControlPlane owns the wait and the identity
// provider's retries, Notifications owns the email's, and neither module reads the other's settings (section 7). Only the host sees
// both, so it checks the rule on start, as each module checks its own settings. It has nothing to bind: the check is all it holds.
internal sealed class InvitationEmailWait
{
    public sealed class Validation(IServiceProvider services) : IValidateOptions<InvitationEmailWait>
    {
        public ValidateOptionsResult Validate(string? name, InvitationEmailWait options)
        {
            var timeout = ControlPlaneInfrastructure.InvitationEmailTimeout(services);
            var identityProvider = ControlPlaneInfrastructure.IdentityProviderRetryDelays(services);
            var email = NotificationsInfrastructure.InvitationEmailRetryDelays(services);
            var retries = identityProvider.Concat(email).Aggregate(TimeSpan.Zero, (sum, delay) => sum + delay);

            return timeout > retries
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(
                    $"{ControlPlaneInfrastructure.InvitationEmailTimeoutKey} ({Format(timeout)}) must be longer than "
                    + $"{ControlPlaneInfrastructure.IdentityProviderRetryDelaysKey} ({Format(identityProvider)}) and "
                    + $"{NotificationsInfrastructure.InvitationEmailRetryDelaysKey} ({Format(email)}) together, {Format(retries)}. "
                    + "Otherwise tenant creation gives up while a retry is still due.");
        }

        private static string Format(TimeSpan time) => time.ToString("c", CultureInfo.InvariantCulture);

        private static string Format(IEnumerable<TimeSpan> times) => string.Join(", ", times.Select(Format));
    }
}

internal static class InvitationEmailWaitExtensions
{
    // Called after the modules are added, since the check reads their settings.
    public static IServiceCollection AddInvitationEmailWait(this IServiceCollection services)
    {
        services.AddOptions<InvitationEmailWait>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<InvitationEmailWait>, InvitationEmailWait.Validation>();

        return services;
    }
}
