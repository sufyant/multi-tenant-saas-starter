using ControlPlane.Contracts.Plans;

namespace Notifications.Application.Plans;

/// <summary>An entitlement's value as a notification stores it (U2): a number, or <c>unlimited</c>, <c>on</c>, <c>off</c> or <c>not_set</c>.</summary>
internal static class EntitlementValues
{
    public static string Of(EntitlementValueRecord value) => value switch
    {
        { Unlimited: true } => "unlimited",
        { Enabled: true } => "on",
        { Enabled: false } => "off",
        { Maximum: { } maximum } => maximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => "not_set",
    };
}
