/** The query parameter that carries the page to return to after sign-in, as Clerk names it. */
export const redirectParameter = "redirect_url";

/**
 * Where to go after sign-in (OWASP, "Unvalidated Redirects and Forwards"): the `redirect_url` when it is a page of this app's own
 * origin, as a path, else `/`. Clerk's `auth.protect()` writes an absolute address of this origin, so an address of this origin
 * passes as its path. Another origin, a protocol-relative `//host`, a backslash and any other scheme fall back to `/`.
 */
export function safeRedirect(value: string | null, origin: string): string {
  if (!value || value.includes("\\")) return "/";
  try {
    const url = new URL(value, origin);
    if (url.origin !== origin) return "/";
    return `${url.pathname}${url.search}${url.hash}`;
  } catch {
    return "/";
  }
}
