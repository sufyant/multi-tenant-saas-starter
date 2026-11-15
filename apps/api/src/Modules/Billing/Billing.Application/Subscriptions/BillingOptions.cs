namespace Billing.Application.Subscriptions;

/// <summary>
/// The settings of billing (section <c>Billing:Payments</c>, docs/configuration.md), checked on start. Billing is a switch of the product's
/// configuration, off by default: while it is off, the app works as before step 40 and billing's endpoints answer 404 <c>billing.off</c>
/// (the maintainer's decision of 2026-11-14; The Twelve-Factor App, "III. Config"). Public because a handler takes it (W9).
/// </summary>
public sealed class BillingOptions
{
    public const string Section = "Billing:Payments";

    /// <summary>Whether billing is on. A product turns it on with its Stripe keys (<c>Billing:Stripe</c>).</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The web app's root, an absolute URL. A page that sends a person to Stripe names its own address to come back to, and only an address
    /// under this root is accepted, so Stripe never sends a person to another site (OWASP, "Unvalidated Redirects and Forwards").
    /// </summary>
    public Uri? WebUrl { get; set; }

    /// <summary>
    /// How long billing's grace runs after the provider's retries of a failed payment are over, before the tenant moves to the default plan
    /// (B11; step 40: "Then 1 week of grace follows").
    /// </summary>
    public TimeSpan Grace { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// How long the provider retries a failed payment, as its own setting holds it (Stripe's Smart Retries, <c>.stripe/README.md</c>): the
    /// Activity log says until when it tries (B11, 12.12). The provider decides the retries; this only names their end.
    /// </summary>
    public TimeSpan RetryWindow { get; set; } = TimeSpan.FromDays(14);

    /// <summary>Whether this address is under <see cref="WebUrl"/>: the same scheme, host and port, and a path under its path.</summary>
    public bool Accepts(Uri address) =>
        WebUrl is { } root && address.IsAbsoluteUri && Uri.Compare(address, root, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0
        && address.AbsolutePath.StartsWith(root.AbsolutePath, StringComparison.Ordinal)
        && string.IsNullOrEmpty(address.Query) && string.IsNullOrEmpty(address.Fragment);
}
