import type { Page } from "@playwright/test";

// Clerk is the real development instance. A frame needs a known state of the identity provider (a connected Google account, the
// sessions), and the web's shared test user gathers a session in every run, so a shot takes Clerk's real answers and changes
// them on the way to the page: every user in an answer gets the account, and the list of sessions gets the frame's devices. The route sits on the page, so it runs
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

/** The client's active session in an answer: the client itself, or the client beside the answer. */
function activeSessionOf(answer: unknown): string | undefined {
  if (!answer || typeof answer !== "object") return undefined;
  const { client, response } = answer as Json;
  const found = [answer, client, response].find(
    (each) =>
      each && typeof each === "object" && (each as Json).object === "client",
  ) as Json | undefined;
  return (found?.last_active_session_id as string | null) ?? undefined;
}

/** Changes every user object of an answer, wherever it sits (the answer itself or the client beside it). */
function withUser(value: unknown, change: (user: Json) => Json): unknown {
  if (Array.isArray(value)) return value.map((each) => withUser(each, change));
  if (!value || typeof value !== "object") return value;
  const object = Object.fromEntries(
    Object.entries(value).map(([key, each]) => [key, withUser(each, change)]),
  );
  return object.object === "user" ? change(object) : object;
}

/** The frame's secret and backup codes of 6.25 and 6.26. */
const totpSecret = "JBSWY3DPEHPK3PXP";
const backupCodes = [
  "4f7k-2m9q",
  "x8pd-63tn",
  "b2hc-q7wv",
  "9rze-5kla",
  "m3ty-8fjd",
  "w6nu-2bpr",
  "k7vs-4dqe",
  "e5ga-9hmx",
  "t2lc-7wzn",
  "p8yf-3kre",
];

/** The authenticator as Clerk's Frontend API gives it: made, then verified with its backup codes. */
const totp = (verified: boolean) => ({
  object: "totp",
  id: "totp_e2e",
  secret: totpSecret,
  uri: `otpauth://totp/Starter:e2e?secret=${totpSecret}&issuer=Starter`,
  verified,
  backup_codes: verified ? backupCodes : null,
  created_at: Date.now(),
  updated_at: Date.now(),
});

/**
 * The identity provider's state of a frame. This device is always Chrome on a Mac, which Clerk names "Macintosh", in Istanbul,
 * active now. With `googleEmail`, Google is connected with that address. With `otherSessions`, as in 6.01 and 9.01, Safari on
 * iPhone in Istanbul two hours ago and Firefox on Windows in Berlin on 3 October follow it; without, as in 6.05, it is alone.
 * With `authenticator`, Set up works without touching the user at Clerk: Clerk's answers make the frame's secret, take any code
 * and give the frame's backup codes, and from then on the person has the authenticator (6.25 to 6.27).
 * The route waits for Clerk's answer, so it can still run when the test ends, and a context that closes under it fails the
 * test. A test file that calls this removes its routes in its `afterEach`: `page.unrouteAll({ behavior: "ignoreErrors" })`.
 */
export async function mockIdentity(
  page: Page,
  {
    googleEmail,
    otherSessions = false,
    authenticator = false,
  }: { googleEmail?: string; otherSessions?: boolean; authenticator?: boolean },
) {
  const fapi = process.env.CLERK_FAPI;
  if (!fapi)
    throw new Error("CLERK_FAPI is not set: run Playwright's global setup.");
  const account = googleEmail ? googleAccount(googleEmail) : null;
  // This browser's session, from the client in Clerk's answers: the shared test user has many, and the list's first is not
  // this one.
  let thisSession: string | undefined;
  let authenticatorOn = false;
  const change = (user: Json): Json => ({
    ...user,
    ...(account && { external_accounts: [account] }),
    ...(authenticatorOn && {
      totp_enabled: true,
      backup_code_enabled: true,
      two_factor_enabled: true,
    }),
  });
  await page.route(
    new RegExp(`^https://${fapi.replaceAll(".", "\\.")}/v1/`),
    async (route) => {
      const url = new URL(route.request().url());
      const token = process.env.CLERK_TESTING_TOKEN;
      if (token) url.searchParams.set(testingToken, token);
      // Clerk sends a change as POST and names the method in `_method`; Set up makes, then verifies, the authenticator.
      const method =
        url.searchParams.get("_method") ?? route.request().method();
      if (authenticator && method === "POST") {
        if (url.pathname.endsWith("/me/totp"))
          return route.fulfill({ json: { response: totp(false) } });
        if (url.pathname.endsWith("/me/totp/attempt_verification")) {
          authenticatorOn = true;
          return route.fulfill({ json: { response: totp(true) } });
        }
      }
      const response = await route.fetch({ url: url.toString() });
      if (!response.headers()["content-type"]?.includes("json"))
        return route.fulfill({ response });
      const answer = await response.json();
      thisSession = activeSessionOf(answer) ?? thisSession;
      let body = withUser(answer, change);
      if (url.pathname.endsWith("/me/sessions/active")) {
        // Clerk answers this list as a bare array, or wrapped in `response` beside the client.
        const wrapped = !Array.isArray(body);
        const list = (wrapped ? (body as Json).response : body) as Json[];
        const current = list.find((each) => each.id === thisSession) ?? list[0];
        if (current) {
          const now = Date.now();
          const thisDevice = activity(
            current,
            current.id as string,
            "Chrome",
            "Macintosh",
            "Istanbul",
            "TR",
            now,
          );
          const sessions = [
            thisDevice,
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
          ].slice(0, otherSessions ? 3 : 1);
          body = wrapped ? { ...(body as Json), response: sessions } : sessions;
        }
      }
      return route.fulfill({ response, json: body });
    },
  );
}
