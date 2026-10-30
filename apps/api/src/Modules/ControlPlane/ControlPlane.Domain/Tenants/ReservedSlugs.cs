using System.Collections.Frozen;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// The slugs no tenant may take. The tenant web's top-level routes share the path with tenant slugs (<c>/{slug}/settings/members</c>),
/// and its router takes a static route first, so a tenant whose slug is a route could never open its pages. A slug never changes, so
/// the list also holds the common words a product adds as a route later. Only the whole slug is reserved: <c>acme-settings</c> is free.
/// The OpenAPI document publishes the list as the enum <c>ReservedSlug</c>, so the web reads it from its generated client (D1).
/// </summary>
public static class ReservedSlugs
{
    /// <summary>Every reserved slug, in alphabetical order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        "about",
        "account",
        "admin",
        "api",
        "app",
        "apps",
        "assets",
        "auth",
        "billing",
        "blog",
        "contact",
        "dashboard",
        "dev",
        "docs",
        "health",
        "help",
        "home",
        "invitations",
        "legal",
        "login",
        "logout",
        "new",
        "notifications",
        "onboarding",
        "platform",
        "pricing",
        "privacy",
        "public",
        "robots-txt",
        "security",
        "settings",
        "sign-in",
        "sign-out",
        "sign-up",
        "sitemap",
        "sso-callback",
        "static",
        "status",
        "support",
        "terms",
        "trpc",
        "www",
    ];

    private static readonly FrozenSet<string> Set = All.ToFrozenSet(StringComparer.Ordinal);

    public static bool Contains(string slug) => Set.Contains(slug);
}
