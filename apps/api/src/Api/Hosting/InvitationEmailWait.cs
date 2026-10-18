using System.Globalization;
using ControlPlane.Infrastructure;
using Microsoft.Extensions.Options;
using Notifications.Infrastructure;

namespace Api.Hosting;

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

// Section 7, configuration rule 5: the wait for the invitation email (section 6, "Tenant creation process") joins the settings of two
// modules, and only the host sees both. It has nothing to bind: the check is all it holds.
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
