using Billing.Application.Checkouts;
using Billing.Application.Customers;
using Billing.Application.Provider;
using Billing.Application.StripeEvents;
using Billing.Application.Subscriptions;
using Billing.Application;
using Billing.Infrastructure.Persistence.Checkouts;
using Billing.Infrastructure.Persistence.Customers;
using Billing.Infrastructure.Persistence.StripeEvents;
using Billing.Infrastructure.Persistence.Subscriptions;
using Billing.Infrastructure.Persistence;
using Billing.Infrastructure.Stripe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stripe;
using Tenancy;
using Wolverine.EntityFrameworkCore;
using Wolverine;

namespace Billing.Infrastructure;

/// <summary>
/// The module's infrastructure entry point: the host calls it to register the module's DbContext, adapters and contract
/// implementations.
/// </summary>
public static class BillingInfrastructure
{
    public static IServiceCollection AddBillingInfrastructure(this IServiceCollection services)
    {
        services.AddDbContextWithWolverineIntegration<BillingDbContext>(
            (provider, options) => options.UseModuleDatabase(provider, BillingDbContext.Schema),
            TenancyServiceCollectionExtensions.MessageSchema);
        services.AddModuleMigrations<BillingDbContext>(BillingDbContext.Schema);

        // Registered by type, so Wolverine builds them in its generated code on the handler's own DbContext (W1).
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ISubscriptionQueries, SubscriptionQueries>();
        services.AddScoped<IStripeEventStore, StripeEventStore>();
        services.AddScoped<ICustomerStore, CustomerStore>();
        services.AddScoped<ICheckoutRepository, CheckoutRepository>();

        // Billing is off by default (the maintainer's decision of 2026-11-14). While it is on, the web's root and Stripe's keys are required.
        services.AddOptions<BillingOptions>()
            .BindConfiguration(BillingOptions.Section)
            .Validate(billing => !billing.Enabled || billing.WebUrl is { IsAbsoluteUri: true }, $"{BillingOptions.Section}:WebUrl must be the web app's absolute root while billing is on.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<BillingOptions>>().Value);
        services.AddOptions<StripeOptions>()
            .BindConfiguration(StripeOptions.Section)
            .Validate<IOptions<BillingOptions>>(
                (stripe, billing) => !billing.Value.Enabled || (!string.IsNullOrWhiteSpace(stripe.SecretKey) && !string.IsNullOrWhiteSpace(stripe.WebhookSecret)),
                $"{StripeOptions.Section}:SecretKey and {StripeOptions.Section}:WebhookSecret must be set while billing is on.")
            .Validate(stripe => stripe.Timeout > TimeSpan.Zero && stripe.WebhookTolerance > TimeSpan.Zero, $"{StripeOptions.Section}:Timeout and WebhookTolerance must be positive.")
            .ValidateOnStart();
        services.AddOptions<BillingProviderOptions>()
            .BindConfiguration(BillingProviderOptions.Section)
            .Validate(settings => settings.IsValid, $"{BillingProviderOptions.Section}:RetryDelays must be positive pauses that grow.")
            .ValidateOnStart();
        services.AddSingleton<IWolverineExtension, BillingProviderRetries>();

        // Wolverine's generated code cannot build the provider, which takes a client that only the container builds, so it resolves it from
        // the message's scope. It reaches no DbContext.
        services.ConfigureWolverine(options => options.CodeGeneration.AlwaysUseServiceLocationFor<IBillingProvider>());
        services.AddHttpClient(nameof(StripeBillingProvider), (provider, http) => http.Timeout = provider.GetRequiredService<IOptions<StripeOptions>>().Value.Timeout);
        services.AddScoped<IStripeClient>(provider => new StripeClient(
            provider.GetRequiredService<IOptions<StripeOptions>>().Value.SecretKey ?? "sk_unset",
            httpClient: new SystemNetHttpClient(provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(StripeBillingProvider)))));
        services.AddScoped<IBillingProvider, StripeBillingProvider>();

        return services;
    }
}
