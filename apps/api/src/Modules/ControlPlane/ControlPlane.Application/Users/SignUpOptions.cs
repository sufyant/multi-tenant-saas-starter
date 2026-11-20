namespace ControlPlane.Application.Users;

/// <summary>
/// The product's sign-up (section <c>ControlPlane:SignUp</c>, step 40d), checked on start; public because a handler takes it (W9). Off by
/// default: only an invitation makes a user. A product turns it on here and in the identity provider's sign-up mode (<c>.clerk/</c>).
/// </summary>
public sealed class SignUpOptions
{
    public const string Section = "ControlPlane:SignUp";

    /// <summary>Whether a person who signed up at the identity provider becomes a user and creates tenants of their own.</summary>
    public bool Enabled { get; set; }

    /// <summary>How many tenants that were not purged one person may create. Positive.</summary>
    public int TenantLimit { get; set; } = 3;
}
