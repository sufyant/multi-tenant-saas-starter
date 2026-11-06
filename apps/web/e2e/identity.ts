import type { Page } from "@playwright/test";

// Clerk is the real development instance, where the test user has one session and no linked account. A frame that needs more
// (a connected Google account, other sessions) takes Clerk's real answers and changes them on the way to the page: every user
// in an answer gets the account, and the list of sessions gets the frame's devices. The route sits on the page, so it runs
// before the testing token's route on the context; it adds the testing token itself, as that route does.

const testingToken = "__clerk_testing_token";
const minute = 60_000;

type Json = Record<string, unknown>;

/** A verified Google account, as Clerk's Frontend API gives it. */
const googleAccount = (email: string) => ({
  object: "external_account",
  id: "eac_e2e_google",
  provider: "oauth_google",
  identification_id: "idn_e2e_google",
  provider_user_id: "e2e-google",
  approved_scopes: "email profile",
  email_address: email,
  first_name: "",
  last_name: "",
  avatar_url: "",
  image_url: "",
  username: null,
  phone_number: null,
  public_metadata: {},
  label: null,
  created_at: Date.now() - 30 * 24 * 60 * minute,
  updated_at: Date.now() - 30 * 24 * 60 * minute,
  verification: {
    object: "verification_oauth",
    status: "verified",
    strategy: "oauth_google",
    attempts: null,
    expire_at: null,
    external_verification_redirect_url: null,
  },
});

/** A device of the frame's Sessions card: the browser, the device, where and when it was last active. */
const activity = (
  session: Json,
  id: string,
  browser: string,
  device: string,
  city: string,
  country: string,
  lastActiveAt: number,
) => ({
  ...session,
  id,
  last_active_at: lastActiveAt,
  latest_activity: {
    ...(session.latest_activity as Json),
    id: `sess_activity_${id}`,
    browser_name: browser,
    device_type: device,
    is_mobile: device === "iPhone",
    city,
    country,
  },
});

/** Gives every user object of an answer the account, wherever it sits (the answer itself or the client beside it). */
function withAccount(value: unknown, account: Json): unknown {
  if (Array.isArray(value))
    return value.map((each) => withAccount(each, account));
  if (!value || typeof value !== "object") return value;
  const object = Object.fromEntries(
    Object.entries(value).map(([key, each]) => [
      key,
      withAccount(each, account),
    ]),
  );
  return object.object === "user"
    ? { ...object, external_accounts: [account] }
    : object;
}

/**
 * The state of the frames 6.01 and 9.01: Google connected with the address given, and three sessions: this device (Chrome on
 * a Mac, which Clerk names "Macintosh", in Istanbul, active now), Safari on iPhone in Istanbul two hours ago, and Firefox on Windows in Berlin on 3 October.
 */
export async function mockIdentity(page: Page, googleEmail: string) {
  const fapi = process.env.CLERK_FAPI;
  if (!fapi)
    throw new Error("CLERK_FAPI is not set: run Playwright's global setup.");
  const account = googleAccount(googleEmail);
  await page.route(
    new RegExp(`^https://${fapi.replaceAll(".", "\\.")}/v1/`),
    async (route) => {
      const url = new URL(route.request().url());
      const token = process.env.CLERK_TESTING_TOKEN;
      if (token) url.searchParams.set(testingToken, token);
      const response = await route.fetch({ url: url.toString() });
      if (!response.headers()["content-type"]?.includes("json"))
        return route.fulfill({ response });
      const body = withAccount(await response.json(), account) as Json;
      if (url.pathname.endsWith("/me/sessions/active")) {
        const [current] = body.response as Json[];
        if (current) {
          const now = Date.now();
          body.response = [
            activity(
              current,
              current.id as string,
              "Chrome",
              "Macintosh",
              "Istanbul",
              "TR",
              now,
            ),
            activity(
              current,
              "sess_e2e_iphone",
              "Safari",
              "iPhone",
              "Istanbul",
              "TR",
              now - 120 * minute,
            ),
            activity(
              current,
              "sess_e2e_windows",
              "Firefox",
              "Windows",
              "Berlin",
              "DE",
              Date.UTC(2026, 9, 3, 14),
            ),
          ];
        }
      }
      return route.fulfill({ response, json: body });
    },
  );
}
