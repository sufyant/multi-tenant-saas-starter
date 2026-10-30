using System.Collections.Frozen;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// The slugs no tenant may take. The tenant web's top-level routes share the path with tenant slugs (<c>/{slug}/settings/members</c>),
/// and its router takes a static route first, so a tenant whose slug is a route could never open its pages. A slug never changes, so
/// the list also holds the common words a product adds as a route later, and the metadata routes of Next.js that have no extension
/// (a root <c>icon.tsx</c> serves <c>/icon</c>). A slug is matched exactly: <c>acme-settings</c> and <c>sign-inn</c> are free, so the
/// web's open routes match whole segments. The OpenAPI document publishes the list as the enum <c>ReservedSlug</c>, so the web reads it
/// from its generated client (D1).
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
        "apple-icon",
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
        "icon",
        "invitations",
        "legal",
        "login",
        "logout",
        "new",
        "notifications",
        "onboarding",
        "opengraph-image",
        "platform",
        "pricing",
        "privacy",
        "public",
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
        "twitter-image",
        "www",
    ];

    private static readonly FrozenSet<string> Set = All.ToFrozenSet(StringComparer.Ordinal);

    public static bool Contains(string slug) => Set.Contains(slug);
}
