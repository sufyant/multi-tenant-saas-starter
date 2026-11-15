namespace Billing.Infrastructure.Stripe;

// Stripe's API (section Billing:Stripe), checked on start while billing is on. The keys come from the environment only, never from a
// committed file.
internal sealed class StripeOptions
{
    public const string Section = "Billing:Stripe";

    /// <summary>The secret key of the Stripe account, <c>sk_test_...</c> or <c>sk_live_...</c>.</summary>
    public string? SecretKey { get; set; }

    /// <summary>The signing secret of the webhook endpoint, <c>whsec_...</c>, which <c>pnpm stripe:apply</c> prints once.</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>The time limit of one call to Stripe (Nygard, <i>Release It!</i>, chapter 5).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>How old a signed event may be, against replays; Stripe's libraries use 5 minutes.</summary>
    public TimeSpan WebhookTolerance { get; set; } = TimeSpan.FromMinutes(5);
}
