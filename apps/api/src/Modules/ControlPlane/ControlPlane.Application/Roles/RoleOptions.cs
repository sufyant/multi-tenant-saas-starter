namespace ControlPlane.Application.Roles;

/// <summary>
/// Configuration of the tenants' roles (section <c>ControlPlane:Roles</c>), checked on start; public because a handler takes it (W9).
/// </summary>
public sealed class RoleOptions
{
    public const string Section = "ControlPlane:Roles";

    /// <summary>
    /// How many custom roles a tenant may have; the built-in roles do not count (the maintainer's decision 3 in step r4b; OWASP API4).
    /// The rule takes the number as a parameter, so a later step may read it from the tenant's plan instead.
    /// </summary>
    public int MaxCustomRolesPerTenant { get; set; } = 20;
}
