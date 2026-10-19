// The rail's width on desktop (F49): its cookie's name and value, in one place, so both apps' layouts read what the AppShell
// writes. The name and the lifetime are shadcn/ui's Sidebar ("Persisted State"); no cookie, or any other value, is the icon rail.

/** The cookie's name. */
export const railCookieName = "sidebar_state";

/** Seven days, as shadcn's Sidebar keeps it. */
export const railCookieMaxAge = 60 * 60 * 24 * 7;

/** Reads the cookie's value: true for the rail with labels. */
export function railExpandedFrom(value: string | undefined): boolean {
  return value === "true";
}

/** The `document.cookie` assignment that saves the choice for the whole origin. */
export function railCookieOf(expanded: boolean): string {
  return `${railCookieName}=${expanded}; path=/; max-age=${railCookieMaxAge}; samesite=lax`;
}
