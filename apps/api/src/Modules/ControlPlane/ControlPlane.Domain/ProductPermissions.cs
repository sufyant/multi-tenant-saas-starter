namespace ControlPlane.Domain;

/// <summary>
/// The product's permissions, beside the template's in <see cref="PermissionPools"/> (X1). The template ships this class empty and never
/// changes it: a product declares its permissions here, as <see cref="PermissionPools"/> declares the template's, and
/// <c>apps/api/scripts/generate-permissions.mjs</c> writes both into the permission list. The Owner role holds every permission of
/// <see cref="Tenant"/> (A2); the Billing manager holds none (step 40).
/// </summary>
internal static class ProductPermissions
{
    /// <summary>The product's permissions of the tenant pool, which a custom role may hold (A3).</summary>
    public static IReadOnlySet<string> Tenant { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The permissions of <see cref="Tenant"/> that the Admin role holds too.</summary>
    public static IReadOnlySet<string> Admin { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The permissions of <see cref="Tenant"/> that the Member role holds too. The Admin role holds each one as well (X2).</summary>
    public static IReadOnlySet<string> Member { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The product's permissions of the platform pool, which every system admin holds (A4).</summary>
    public static IReadOnlySet<string> Platform { get; } = new HashSet<string>(StringComparer.Ordinal);
}
