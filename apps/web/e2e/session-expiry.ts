import type { Page } from "@playwright/test";

/** The values of the session cookies that the page holds now: `__session` and, on a development instance, its suffixed twin. */
const sessionTokens = async (page: Page) =>
  (await page.context().cookies())
    .filter((cookie) => cookie.name.startsWith("__session"))
    .map((cookie) => cookie.value);

/**
 * The page's session token expires before clerk-js refreshes it (F63): the browser's clock moves on 70 seconds, past the token's
 * 60, and a client-side navigation to `path` that still carries the old token gets the proxy's answer to an expired one, 401. The
 * real proxy cannot see the browser's clock, so this route answers for it. The test installs the clock before it opens the page.
 */
export async function expireTheSession(page: Page, path: string) {
  const old = await sessionTokens(page);
  await page.route(
    (url) => url.pathname === path && url.searchParams.has("_rsc"),
    async (route) => {
      const cookie = (await route.request().headerValue("cookie")) ?? "";
      return old.some((token) => cookie.includes(token))
        ? route.fulfill({ status: 401 })
        : route.fallback();
    },
  );
  await page.clock.setSystemTime(Date.now() + 70_000);
}
