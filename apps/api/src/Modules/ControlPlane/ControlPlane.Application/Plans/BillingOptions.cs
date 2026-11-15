namespace ControlPlane.Application.Plans;

/// <summary>
/// The settings of billing (section <c>ControlPlane:Billing</c>, docs/configuration.md), checked on start; public because a handler takes
/// it (W9).
/// </summary>
public sealed class BillingOptions
{
    public const string Section = "ControlPlane:Billing";

    /// <summary>
    /// The deployment's one currency, an ISO 4217 code in capitals. Every plan and price is in it, because a Stripe Price and a subscription
    /// have one currency (Stripe, "Supported currencies"; the maintainer's decision of 2026-11-14).
    /// </summary>
    public string Currency { get; set; } = "EUR";
}
