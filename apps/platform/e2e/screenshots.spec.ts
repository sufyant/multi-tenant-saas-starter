import { randomUUID } from "node:crypto";
import { setupClerkTestingToken } from "@clerk/testing/playwright";
import { expect, type Page, test } from "@playwright/test";
import {
  AuditActorResponseKind,
  type AuditEntryResponse,
  AuditOperation,
  AuditTargetResponseKind,
  type CursorPageOfAuditEntryResponse,
  type EntitlementValueResponse,
  ErrorCode,
  FeatureFlagAddedToCodeDetailsResponseOperation,
  FeatureFlagPercentageSetDetailsResponseOperation,
  FeatureFlagRemovedFromCodeDetailsResponseOperation,
  FeatureFlagTenantListedDetailsResponseOperation,
  FeatureFlagTenantUnlistedDetailsResponseOperation,
  FeatureKey,
  Language,
  LimitKey,
  NotificationLinkResponseKind,
  NotificationType,
  type PlanResponse,
  TenantCreatedDetailsResponseOperation,
  TenantEntitlementOverrideSetDetailsResponseOperation,
  TenantPlanChangedDetailsResponseOperation,
  TenantPurgeWarnedDetailsResponseOperation,
  TermsVersionCancelledDetailsResponseOperation,
  TermsVersionCreatedDetailsResponseOperation,
  TermsVersionUpdatedDetailsResponseOperation,
  type TrialResponse,
} from "@repo/api-client";
import { localeCookie } from "@repo/ui/lib/locale";
import {
  mockNotifications,
  mockPlatform,
  mockRename,
  organisations,
  plans,
  problem,
  shannonSuspended,
  tenantsPage,
  users,
} from "./api";
import { createUser, deleteUser } from "./clerk-users";
import {
  emailsOff,
  flagsFail,
  flagsWait,
  frameDetails,
  mockFrameFlags,
} from "./flag-frames";
import {
  frameAdmins,
  frameUsers,
  mockFrameGrant,
  mockFrameRevoke,
  mockFrameUsers,
  usersFail,
  usersWait,
} from "./frame-users";
import { mockIdentity } from "./identity";
import {
  frameNorthwind,
  frameOrganisations,
  freePreview,
  listItemOf,
  mockFrameOrganisations,
} from "./organisation-frames";
import {
  frameTerms,
  mockFrameAdd,
  mockFrameCancel,
  mockFrameTerms,
  termsFail,
  termsWait,
} from "./terms-frames";
import { enrolledUser, signInWithEmailCode, totpCode } from "./two-step";

/** The bell's tabs; a page may have tabs of its own. */
const bellTabs = (page: Page) =>
  page.getByRole("tablist", { name: "Which notifications" });

// Screenshots for a pull request. They run only with E2E_SCREENSHOTS=1 and write to apps/platform/test-results/screenshots,
// which Git ignores and Playwright empties at the start of each run. Attach them to the pull request; never commit them.
test.skip(
  !process.env.E2E_SCREENSHOTS,
  "Set E2E_SCREENSHOTS=1 to take the screenshots.",
);

// A route that reads Clerk's answer (e2e/identity.ts) can still wait for it when the test ends. The context then closes under
// it, the read fails, and Playwright fails the finished test with that error. So each test removes its routes before the
// context closes, and a route still running ends without an error. Waiting for those routes instead would hang: a loading
// shot holds its answer on purpose.
test.afterEach(({ page }) => page.unrouteAll({ behavior: "ignoreErrors" }));

const folder = "test-results/screenshots";
const sizes = {
  desktop: { width: 1440, height: 900 },
  tablet: { width: 834, height: 1112 },
  phone: { width: 390, height: 844 },
};
const variants = [
  { language: Language.En, scheme: "light", size: "desktop" },
  { language: Language.En, scheme: "dark", size: "desktop" },
  { language: Language.Tr, scheme: "light", size: "tablet" },
  { language: Language.Ar, scheme: "light", size: "phone" },
] as const;

// The platform's sections: Organisations, Users, System admins, Plans and Activity, with one organisation, one user and one plan.
const pages = [
  ["organisations", "/"],
  ["organisation", `/organisations/${organisations.atlas.id}`],
  ["users", "/users"],
  ["user", `/users/${users.ayse.id}`],
  ["system-admins", "/settings/system-admins"],
  ["plans", "/plans"],
  ["plan", `/plans/${plans.pro.id}`],
  ["plan-default", `/plans/${plans.free.id}`],
  ["plan-new", "/plans/new"],
  ["organisation-plan", `/organisations/${organisations.northwind.id}`],
  ["activity", "/activity"],
] as const;

for (const { language, scheme, size } of variants)
  for (const [name, path] of pages)
    test(`Screenshot_${name}-${language}-${scheme}-${size}`, async ({
      page,
      context,
      baseURL,
    }) => {
      await setupClerkTestingToken({ page });
      await page.emulateMedia({ colorScheme: scheme });
      await page.setViewportSize(sizes[size]);
      // The language's cookie (F13), so the first render is already in it.
      await context.addCookies([
        { name: localeCookie, value: language, url: baseURL },
      ]);
      await mockPlatform(page, language);

      await page.goto(path);
      await page.getByRole("main").getByRole("heading").first().waitFor();
      await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);

      await page.screenshot({
        path: `${folder}/${name}-${language}-${scheme}-${size}.png`,
        fullPage: true,
      });
    });

// The account menu open: English light, Arabic dark and a phone, where it opens from the drawer.
const menus = [
  {
    language: Language.En,
    scheme: "light",
    size: "desktop",
    name: "Sam Kelly",
  },
  { language: Language.Ar, scheme: "dark", size: "desktop", name: "Sam Kelly" },
  { language: Language.En, scheme: "light", size: "phone", name: "Sam Kelly" },
] as const;

for (const { language, scheme, size, name } of menus)
  test(`Screenshot_user-menu-${language}-${scheme}-${size}`, async ({
    page,
    context,
    baseURL,
  }) => {
    await setupClerkTestingToken({ page });
    await page.emulateMedia({ colorScheme: scheme });
    await page.setViewportSize(sizes[size]);
    await context.addCookies([
      { name: localeCookie, value: language, url: baseURL },
    ]);
    await mockPlatform(page, language);

    await page.goto("/users");
    await page.getByRole("main").getByRole("heading").first().waitFor();
    if (size === "phone")
      await page.getByRole("button", { name: "Open menu" }).click();
    await page.getByRole("button", { name, exact: true }).click();
    await page.getByRole("menu").waitFor();

    await page.screenshot({
      path: `${folder}/user-menu-${language}-${scheme}-${size}.png`,
    });
  });

// The platform's bell and its list (the Notifications boards).
test("Screenshot_Notifications_EnglishLightDesktop", async ({ page }) => {
  await setupClerkTestingToken({ page });
  await page.emulateMedia({ colorScheme: "light" });
  await page.setViewportSize({ width: 1440, height: 900 });
  await mockPlatform(page);
  await mockNotifications(page);

  await page.goto("/");
  await page.getByRole("button", { name: "Notifications, 1 unread" }).click();
  await page.getByRole("link", { name: /was suspended/ }).waitFor();

  await page.screenshot({
    path: `${folder}/notifications-en-light-desktop.png`,
  });
});

// The shell at the sizes of the design's frames (the platform's Shell and Notifications screens), for the comparison in each pull
// request: CI uploads them as the workflow artifact "screenshots" (scripts/ci/test-e2e.sh). The name gives the frame.
const frames = {
  desktop: { width: 1280, height: 800 },
  tablet: { width: 1024, height: 768 },
  phone: { width: 375, height: 812 },
};
/** The platform's Notifications frames: two unread items and one read item, each with its area and the person who acted. */
const ago = (minutes: number) =>
  new Date(Date.now() - minutes * 60_000).toISOString();
const bellList = {
  unread: 2,
  items: [
    { ...shannonSuspended, createdAt: ago(20) },
    {
      ...shannonSuspended,
      id: "0198f0a2-0000-7000-8000-0000000000b2",
      type: NotificationType.TenantOwnerJoined,
      title: "Northwind Traders has its Owner",
      actorName: "Ayşe Demir",
      tenantName: "Northwind Traders",
      createdAt: ago(24 * 60),
    },
    {
      ...shannonSuspended,
      id: "0198f0a2-0000-7000-8000-0000000000b3",
      type: NotificationType.TenantClosed,
      title: "Fernwood Studio was closed",
      actorName: null,
      tenantName: "Fernwood Studio",
      createdAt: ago(3 * 24 * 60),
      readAt: ago(2 * 24 * 60),
      unread: false,
    },
  ],
};
/** The weekly summary of the flags (3.07), unread above the bell's other items. */
const flagsBell = {
  unread: 3,
  items: [
    {
      ...shannonSuspended,
      id: "0198f0a2-0000-7000-8000-0000000000b9",
      type: NotificationType.FeatureFlagsReadyToRemove,
      title: "2 feature flags are ready to be removed from code",
      area: "Feature flags",
      actorName: null,
      tenantName: null,
      link: {
        kind: NotificationLinkResponseKind.PlatformFeatureFlags,
        tenantId: null,
      },
      createdAt: ago(60),
    },
    ...bellList.items.map((item) => ({ ...item, unread: true, readAt: null })),
  ],
};
/** The Name field of the Profile form. */
const nameField = (page: Page) => page.getByRole("textbox", { name: "Name" });

/** Adds a word to the name, so Save changes turns on (5.02, 8.02). */
const editName = async (page: Page) => {
  await nameField(page).press("End");
  await nameField(page).pressSequentially(" Demir");
};

/** Saves the name; the mocked RenameMe answers, and the toast says so (5.04, 8.04). */
const saveName = async (page: Page) => {
  await editName(page);
  await page.getByRole("button", { name: "Save changes" }).click();
  await page.getByText("Profile saved").waitFor();
};

/** Saves an empty name, so the field says to enter one (5.03, 8.03). */
const emptyName = async (page: Page) => {
  await nameField(page).fill("");
  await page.getByRole("button", { name: "Save changes" }).click();
  await page.getByText("Enter your name.").waitFor();
};

/** Scrolls the theme's card, the one radio group of the Profile page, into view. */
const showTheme = (page: Page) =>
  page.getByRole("radiogroup").scrollIntoViewIfNeeded();

/** The state of the frames that show Google connected and two other sessions (e2e/identity.ts). */
const fullState = (page: Page) =>
  mockIdentity(page, {
    googleEmail: "deniz.kaya@gmail.example",
    otherSessions: true,
  });
/** Waits until the sessions show the last of the other devices. */
const otherSessionsShown = (page: Page) =>
  page.getByText("Firefox on Windows", { exact: true }).waitFor();

/** Opens Change email; with an address, sends it, and the mocked API answers that the code works for 10 minutes (6.08, 6.09). */
const changeEmail = (address?: string) => async (page: Page) => {
  await otherSessionsShown(page);
  await page.route("**/api/v1/me/email-change", (route) =>
    route.fulfill({
      status: 201,
      json: { expiresAt: new Date(Date.now() + 600_000).toISOString() },
    }),
  );
  await page.getByRole("button", { name: "Change email" }).click();
  if (!address) return;
  await page.getByRole("textbox", { name: "New email" }).fill(address);
  await page.getByRole("button", { name: "Send code" }).click();
  await page.getByRole("alertdialog", { name: "Check your email" }).waitFor();
};

// The Plans frames (6.01 to 6.19): Free, the default; Team; Business, each created on 1 Jan 2026, with the values the frames draw.
// Each keeps the Activity log for 365 days (6.04, 6.05, 6.18).
const value = (
  values: Partial<EntitlementValueResponse>,
): EntitlementValueResponse => ({
  enabled: null,
  maximum: null,
  unlimited: false,
  ...values,
});
const framePlan = (
  id: string,
  name: string,
  tenantCount: number,
  entitlements: PlanResponse["entitlements"],
  isDefault = false,
): PlanResponse => ({
  id,
  name,
  isDefault,
  tenantCount,
  entitlements,
  currency: "EUR",
  prices: {
    baseMonthly: 0,
    perPersonMonthly: 0,
    baseYearly: 0,
    perPersonYearly: 0,
  },
  meters: [],
  createdAt: "2026-01-01T09:00:00Z",
});
const framePlans = {
  free: framePlan(
    "0198f0a2-0000-7000-8000-00000000c101",
    "Free",
    1,
    {
      [FeatureKey.RolesCustom]: value({ enabled: false }),
      [LimitKey.MembersMax]: value({ maximum: 3 }),
      [LimitKey.AuditRetentionDays]: value({ maximum: 365 }),
    },
    true,
  ),
  team: framePlan("0198f0a2-0000-7000-8000-00000000c102", "Team", 5, {
    [FeatureKey.RolesCustom]: value({ enabled: true }),
    [LimitKey.RolesCustomMax]: value({ maximum: 5 }),
    [LimitKey.MembersMax]: value({ maximum: 15 }),
    [LimitKey.AuditRetentionDays]: value({ maximum: 365 }),
  }),
  business: framePlan("0198f0a2-0000-7000-8000-00000000c103", "Business", 1, {
    [FeatureKey.RolesCustom]: value({ enabled: true }),
    [LimitKey.RolesCustomMax]: value({ maximum: 20 }),
    [LimitKey.MembersMax]: value({ unlimited: true }),
    [LimitKey.AuditRetentionDays]: value({ maximum: 365 }),
  }),
};
const nonprofit = framePlan(
  "0198f0a2-0000-7000-8000-00000000c104",
  "Nonprofit",
  0,
  {
    [FeatureKey.RolesCustom]: value({ enabled: false }),
    [LimitKey.MembersMax]: value({ maximum: 10 }),
    [LimitKey.AuditRetentionDays]: value({ maximum: 365 }),
  },
);
const teamPath = `/plans/${framePlans.team.id}`;

/** ListPlans and GetPlan answer the frames' plans; a command of the plans goes on to the shot's own route. */
const mockFramePlans = async (page: Page) => {
  const all = [...Object.values(framePlans), nonprofit];
  await page.route("**/api/v1/platform/plans*", (route) =>
    route.request().method() === "GET"
      ? route.fulfill({
          json: {
            items: Object.values(framePlans).map(
              ({ entitlements: _, ...item }) => item,
            ),
            page: 1,
            pageSize: 50,
            total: 3,
          },
        })
      : route.fallback(),
  );
  await page.route("**/api/v1/platform/plans/*", (route) => {
    if (route.request().method() !== "GET") return route.fallback();
    const id = new URL(route.request().url()).pathname.split("/").at(-1);
    const found = all.find((item) => item.id === id);
    return found ? route.fulfill({ json: found }) : route.fallback();
  });
};

/** ListPlans answers so, after the frames' plans. */
const plansAnswer =
  (answer: (route: Parameters<Parameters<Page["route"]>[1]>[0]) => unknown) =>
  async (page: Page) => {
    await mockFramePlans(page);
    await page.route("**/api/v1/platform/plans*", (route) =>
      route.request().method() === "GET" ? answer(route) : route.fallback(),
    );
  };

/** The preview of Team's edit: two organisations go over the seats (6.07), none, or it fails (6.09). */
const teamPreview =
  (answer: "over" | "none" | "fails") => async (page: Page) => {
    await mockFramePlans(page);
    await page.route(
      `**/api/v1/platform/plans/${framePlans.team.id}/preview*`,
      (route) =>
        answer === "fails"
          ? route.fulfill(problem(500, ErrorCode.ServerError))
          : route.fulfill({
              json: {
                items:
                  answer === "none"
                    ? []
                    : [
                        [organisations.northwind, 12, 6],
                        [
                          {
                            ...organisations.northwind,
                            id: "0198f0a2-0000-7000-8000-00000000a0f1",
                            name: "شركة الأفق للتجارة",
                            slug: "alufuq",
                          },
                          8,
                          2,
                        ],
                      ].map(([organisation, count, amount]) => {
                        const { id, name, slug } =
                          organisation as typeof organisations.northwind;
                        return {
                          tenantId: id,
                          name,
                          slug,
                          furthest: amount,
                          excesses: [
                            {
                              key: LimitKey.MembersMax,
                              count,
                              newValue: value({ maximum: 6 }),
                              amount,
                            },
                          ],
                          overriddenKeys: [],
                        };
                      }),
                page: 1,
                pageSize: 20,
                total: answer === "none" ? 0 : 2,
              },
            }),
    );
    await page.route(
      `**/api/v1/platform/plans/${framePlans.team.id}`,
      (route) =>
        route.request().method() === "PATCH"
          ? route.fulfill({
              json: {
                ...framePlans.team,
                entitlements: {
                  ...framePlans.team.entitlements,
                  [LimitKey.MembersMax]: value({ maximum: 6 }),
                },
              },
            })
          : route.fallback(),
    );
  };

/** The preview of Team's edit from 365 to 90 days: it shortens the Activity log of 4 organisations, and puts none over (6.19). */
const teamShortens = async (page: Page) => {
  await mockFramePlans(page);
  await page.route(
    `**/api/v1/platform/plans/${framePlans.team.id}/preview*`,
    (route) =>
      route.fulfill({
        json: {
          items: [],
          page: 1,
          pageSize: 20,
          total: 0,
          retentionShortened: { count: 4, fromDays: 365, toDays: 90 },
        },
      }),
  );
};

/** Team with Custom roles turned off and its stored limit of 5 kept (6.25). */
const teamCustomRolesOff = async (page: Page) => {
  await mockFramePlans(page);
  await page.route(`**/api/v1/platform/plans/${framePlans.team.id}`, (route) =>
    route.request().method() === "GET"
      ? route.fulfill({
          json: {
            ...framePlans.team,
            entitlements: {
              ...framePlans.team.entitlements,
              [FeatureKey.RolesCustom]: value({ enabled: false }),
            },
          },
        })
      : route.fallback(),
  );
};

/** Turns Custom roles off in the plan editor, the one switch of the form (6.21 to 6.24). */
const turnCustomRolesOff = (page: Page) => page.getByRole("switch").click();

/** The Activity log's field of the plan editor (6.04). */
const keepsEntriesField = (page: Page) =>
  page.getByRole("group", { name: "Keeps entries" }).getByRole("spinbutton");

/** The Seats field of the plan editor. */
const seatsField = (page: Page) =>
  page.getByRole("group", { name: "Seats" }).getByRole("spinbutton");

/** Seats from 15 to 6, focused (6.06). */
const lowerTheSeats = (page: Page) => seatsField(page).fill("6");

/** Lowers the seats and saves: the preview runs first, because the edit takes something away. */
const saveLowerSeats = async (page: Page) => {
  await lowerTheSeats(page);
  await page.getByRole("button", { name: "Save changes" }).click();
};

/** CreatePlan answers so, and records nothing. */
const createAnswers =
  (answer: (route: Parameters<Parameters<Page["route"]>[1]>[0]) => unknown) =>
  async (page: Page) => {
    await mockFramePlans(page);
    await page.route("**/api/v1/platform/plans", (route) =>
      route.request().method() === "POST" ? answer(route) : route.fallback(),
    );
  };

/** Add override on Northwind's Activity log: the value, until 31 Jan 2027, and the reason (4.16, 4.49). */
const overrideActivityLog =
  (days: string, reason: string) => async (page: Page) => {
    await page.getByRole("button", { name: "Add override" }).click();
    const dialog = page.getByRole("alertdialog");
    await dialog.getByRole("combobox", { name: "Feature or limit" }).click();
    await page.getByRole("option", { name: "Activity log" }).click();
    await page.getByRole("listbox").waitFor({ state: "hidden" });
    await dialog.getByRole("spinbutton", { name: "Value" }).fill(days);
    await dialog.getByLabel("Until").fill("2027-01-31");
    await dialog.getByRole("textbox", { name: "Reason" }).fill(reason);
  };

/** Reduced motion stops a skeleton's pulse, so a loading frame is the same in every run. */
const reduceMotion = (page: Page) =>
  page.emulateMedia({ reducedMotion: "reduce" });

// The Activity log frames (7.01 to 7.08): the platform's own sentences of 7.01, by Deniz Kaya, on the days the frame draws them
// from today. The fee, trial and Contract sentences are step 40's.
const minutesAgo = (minutes: number) =>
  new Date(Date.now() - minutes * 60_000).toISOString();
const deniz = {
  kind: AuditActorResponseKind.User,
  userId: "0198f0a2-0000-7000-8000-00000000b0d1",
  displayName: "Deniz Kaya",
  deleted: false,
};
const theTenant = (name: string) => ({
  kind: AuditTargetResponseKind.Tenant,
  id: organisations.northwind.id,
  name,
  deleted: null,
  email: null,
});
const logEntry = (
  id: number,
  daysAgo: number,
  operation: AuditOperation,
  fields: Partial<AuditEntryResponse>,
): AuditEntryResponse => ({
  id: `0198f0a2-0000-7000-8000-0000000e${String(id).padStart(4, "0")}`,
  occurredAt: minutesAgo(daysAgo * 24 * 60 + id),
  operation,
  actor: deniz,
  target: theTenant("Northwind Traders"),
  details: null,
  ...fields,
});
const frameLog: AuditEntryResponse[] = [
  logEntry(1, 0, AuditOperation.TenantCreated, {
    target: theTenant("Harbourside Dental"),
    details: {
      operation: TenantCreatedDetailsResponseOperation.TenantCreated,
      plan: { id: framePlans.free.id, name: "Free" },
    },
  }),
  logEntry(2, 1, AuditOperation.TenantPurgeWarned, {
    actor: {
      kind: AuditActorResponseKind.Platform,
      userId: null,
      displayName: null,
      deleted: null,
    },
    target: theTenant("Fernwood Studio"),
    details: {
      operation: TenantPurgeWarnedDetailsResponseOperation.TenantPurgeWarned,
      purgesAt: minutesAgo(-29 * 24 * 60),
    },
  }),
  logEntry(3, 5, AuditOperation.TenantEntitlementOverrideSet, {
    details: {
      operation:
        TenantEntitlementOverrideSetDetailsResponseOperation.TenantEntitlementOverrideSet,
      key: LimitKey.RolesCustomMax,
      oldValue: value({ maximum: 5 }),
      oldExpiresAt: null,
      newValue: value({ maximum: 10 }),
      newExpiresAt: minutesAgo(-54 * 24 * 60),
      reason: "pilot",
    },
  }),
  logEntry(4, 8, AuditOperation.UserSuspended, {
    target: {
      kind: AuditTargetResponseKind.User,
      id: "0198f0a2-0000-7000-8000-00000000b0e1",
      name: "Mark Ellis",
      deleted: false,
      email: null,
    },
  }),
  logEntry(5, 13, AuditOperation.TenantPlanChanged, {
    target: theTenant("Pacific Coast Freight & Warehousing Cooperative Ltd."),
    details: {
      operation: TenantPlanChangedDetailsResponseOperation.TenantPlanChanged,
      oldPlan: { id: framePlans.team.id, name: "Team" },
      newPlan: { id: framePlans.business.id, name: "Business" },
    },
  }),
  logEntry(6, 13, AuditOperation.UserPreferencesChanged, {
    actor: { ...deniz, userId: null, displayName: null, deleted: true },
    target: {
      kind: AuditTargetResponseKind.User,
      id: "0198f0a2-0000-7000-8000-00000000b0e2",
      name: null,
      deleted: true,
      email: null,
    },
  }),
  logEntry(7, 21, AuditOperation.TenantPurged, {
    actor: {
      kind: AuditActorResponseKind.Platform,
      userId: null,
      displayName: null,
      deleted: null,
    },
    target: theTenant("Greyfield Books"),
  }),
];

/** The log answers its first page with these entries and a next cursor; the next page answers as the shot says. */
/** A day the form accepts in every run: 1 Dec of next year. The mocked answer still adds the frames' 1 Dec 2026 (11.04). */
const nextYearsDay = `${new Date().getUTCFullYear() + 1}-12-01`;

/** The four kinds of sentence of a terms version (7.09), by Deniz Kaya. */
const termsTarget = {
  kind: AuditTargetResponseKind.TermsVersion,
  id: frameTerms.scheduled.id,
  name: null,
  deleted: false,
  email: null,
};
const termsValues = {
  effectiveDate: "2026-12-15",
  url: "https://starter.example/terms/2026-12",
  summary: null,
  requiresAcceptance: true,
  sendsEmail: true,
};
const termsLog: AuditEntryResponse[] = [
  logEntry(1, 0, AuditOperation.TermsVersionCancelled, {
    target: termsTarget,
    details: {
      operation:
        TermsVersionCancelledDetailsResponseOperation.TermsVersionCancelled,
      effectiveDate: "2026-12-15",
      url: termsValues.url,
      requiresAcceptance: true,
      acceptanceCount: 18,
    },
  }),
  logEntry(2, 1, AuditOperation.TermsVersionUpdated, {
    target: termsTarget,
    details: {
      operation:
        TermsVersionUpdatedDetailsResponseOperation.TermsVersionUpdated,
      old: { ...termsValues, effectiveDate: "2026-12-01" },
      new: termsValues,
    },
  }),
  logEntry(3, 2, AuditOperation.TermsVersionCreated, {
    target: termsTarget,
    details: {
      operation:
        TermsVersionCreatedDetailsResponseOperation.TermsVersionCreated,
      ...termsValues,
      effectiveDate: "2026-12-01",
    },
  }),
  logEntry(4, 3, AuditOperation.TermsVersionCreated, {
    actor: {
      ...deniz,
      userId: "0198f0a2-0000-7000-8000-00000000b0d2",
      displayName: "Hannah Weber",
    },
    target: { ...termsTarget, id: frameTerms.inForce.id },
    details: {
      operation:
        TermsVersionCreatedDetailsResponseOperation.TermsVersionCreated,
      ...termsValues,
      effectiveDate: "2026-11-01",
      requiresAcceptance: false,
    },
  }),
];

/** A flag as the target of an entry: its key is its name (7.10). */
const flagTarget = (key: string) => ({
  kind: AuditTargetResponseKind.FeatureFlag,
  id: "0198f0a2-0000-7000-8000-0000000000a9",
  name: key,
  deleted: null,
  email: null,
});
const deploy = {
  kind: AuditActorResponseKind.Platform,
  userId: null,
  displayName: null,
  deleted: null,
};
/** The seven sentences of 7.10. */
const flagsLog: AuditEntryResponse[] = [
  logEntry(1, 0, AuditOperation.FeatureFlagEveryoneElseOn, {
    target: flagTarget("emails.send"),
  }),
  logEntry(2, 0, AuditOperation.FeatureFlagPercentageSet, {
    target: flagTarget("roles.bulk_edit"),
    details: {
      operation:
        FeatureFlagPercentageSetDetailsResponseOperation.FeatureFlagPercentageSet,
      from: 10,
      to: 50,
    },
  }),
  logEntry(3, 0, AuditOperation.FeatureFlagEveryoneElseOff, {
    target: flagTarget("emails.send"),
  }),
  logEntry(4, 1, AuditOperation.FeatureFlagTenantUnlisted, {
    actor: { ...deniz, displayName: "Hannah Weber" },
    target: flagTarget("members.new_table"),
    details: {
      operation:
        FeatureFlagTenantUnlistedDetailsResponseOperation.FeatureFlagTenantUnlisted,
      tenantId: organisations.northwind.id,
      tenantName: "Blue Harbour",
    },
  }),
  logEntry(5, 2, AuditOperation.FeatureFlagTenantListed, {
    target: flagTarget("roles.bulk_edit"),
    details: {
      operation:
        FeatureFlagTenantListedDetailsResponseOperation.FeatureFlagTenantListed,
      tenantId: organisations.northwind.id,
      tenantName: "Northwind Traders",
    },
  }),
  logEntry(6, 3, AuditOperation.FeatureFlagRemovedFromCode, {
    actor: deploy,
    target: flagTarget("invitations.csv_import"),
    details: {
      operation:
        FeatureFlagRemovedFromCodeDetailsResponseOperation.FeatureFlagRemovedFromCode,
      revision: "7c41d09",
    },
  }),
  logEntry(7, 4, AuditOperation.FeatureFlagAddedToCode, {
    actor: deploy,
    target: flagTarget("roles.bulk_edit"),
    details: {
      operation:
        FeatureFlagAddedToCodeDetailsResponseOperation.FeatureFlagAddedToCode,
      revision: "3f9e21c",
      defaultValue: false,
    },
  }),
];
/** A flag's page of the frames. */
const frameDetailOf = (key: string) => {
  const found = frameDetails[key];
  if (!found) throw new Error(`No frame flag ${key}.`);
  return found;
};
const logAnswers =
  (
    first: AuditEntryResponse[],
    next?: (route: Parameters<Parameters<Page["route"]>[1]>[0]) => unknown,
  ) =>
  (page: Page) =>
    page.route("**/api/v1/platform/audit-log*", (route) => {
      const cursor = new URL(route.request().url()).searchParams.get("cursor");
      if (cursor && next) return next(route);
      const answer: CursorPageOfAuditEntryResponse = {
        items: first,
        nextCursor: next ? "older" : null,
      };
      return route.fulfill({ json: answer });
    });

/** Clicks Load older activity. */
const loadOlder = (page: Page) =>
  page.getByRole("button", { name: "Load older activity" }).click();

// The Organisations frames' helpers: the organisation's page, its menu, and the Create dialog filled as 4.07 draws it.
const pathOf = (organisation: { id: string }) =>
  `/organisations/${organisation.id}`;
const northwindPath = pathOf(frameNorthwind);
const openMenu = (name: string) => async (page: Page) => {
  await page.getByRole("button", { name: `Actions for ${name}` }).click();
  await page.getByRole("menu").waitFor();
};
const chooseAction = (name: string, action: string) => async (page: Page) => {
  await openMenu(name)(page);
  await page.getByRole("menuitem", { name: action }).click();
  await page.getByRole("alertdialog").waitFor();
};
const fillCreate = (slug: string) => async (page: Page) => {
  await page
    .getByRole("main")
    .getByRole("button", { name: "Create organisation" })
    .click();
  const dialog = page.getByRole("alertdialog");
  await dialog
    .getByRole("textbox", { name: "Name" })
    .fill("Harbourside Dental");
  await dialog.getByRole("textbox", { name: "Address" }).fill(slug);
  await dialog
    .getByRole("textbox", { name: "Owner’s email" })
    .fill("maya.chen@harbourside.example");
};

/** Opens an action of the user's header menu (5.06). */
const userAction = (name: string, action: string) => async (page: Page) => {
  await page.getByRole("button", { name: `Actions for ${name}` }).click();
  await page.getByRole("menuitem", { name: action }).click();
  await page.getByRole("alertdialog").waitFor();
};

/** Add system admin with an email typed, once the lookup has answered (10.02 to 10.04). */
const addAdmin = (email: string) => async (page: Page) => {
  await page.getByRole("button", { name: "Add system admin" }).click();
  const lookup = page.waitForResponse((response) =>
    response.url().includes(`email=${encodeURIComponent(email)}`),
  );
  await page
    .getByRole("alertdialog")
    .getByRole("textbox", { name: "Email" })
    .fill(email);
  await lookup;
};

/** Add's second step for Omar (10.06). */
const confirmOmar = async (page: Page) => {
  await addAdmin("omar@northwind.example")(page);
  await page.getByRole("button", { name: "Continue" }).click();
  await page
    .getByRole("alertdialog", { name: "Make Omar Haddad a system admin?" })
    .waitFor();
};

/** Remove system admin from Hannah's row (10.10). */
const removeHannah = async (page: Page) => {
  await page.getByRole("button", { name: "Actions for Hannah Weber" }).click();
  await page.getByRole("menuitem", { name: "Remove system admin" }).click();
  await page.getByRole("alertdialog").waitFor();
};

/** Team at €10 per person a month (6.13 to 6.16). */
/**
 * Team at €10 per person a month and €100 a year, with a meter of the tests' own (6.13; the template ships none, and "AI requests"
 * is the frame's sample data). The code's meters are the plan's, and "storage" beside it for the meter that the plan does not set
 * (6.26, 6.27).
 */
const teamPricedAndMetered = async (page: Page, code = ["ai.requests"]) => {
  await mockFramePlans(page);
  await page.route("**/api/v1/platform/meters", (route) =>
    route.fulfill({
      json: { meters: code.map((key) => ({ key, description: key })) },
    }),
  );
  await page.route(`**/api/v1/platform/plans/${framePlans.team.id}`, (route) =>
    route.request().method() === "GET"
      ? route.fulfill({
          json: {
            ...framePlans.team,
            prices: {
              baseMonthly: 0,
              baseYearly: 0,
              perPersonMonthly: 1000,
              perPersonYearly: 10000,
            },
            meters: [
              {
                key: "ai.requests",
                quota: 10000,
                overage: "charge",
                unitPrice: 100,
                unitSize: 1000,
              },
            ],
          },
        })
      : route.fallback(),
  );
};

/** The monthly fee per person edited to €12, as 6.13 draws it. */
const editPerPersonMonthly = async (page: Page) => {
  const field = page.getByRole("textbox", { name: "Per person, monthly" });
  await field.fill("12");
  await page.getByText("Today €10. Changed.", { exact: false }).waitFor();
};

const teamAtTen = async (page: Page) => {
  await mockFramePlans(page);
  await page.route(`**/api/v1/platform/plans/${framePlans.team.id}`, (route) =>
    route.request().method() === "GET"
      ? route.fulfill({
          json: {
            ...framePlans.team,
            prices: {
              baseMonthly: 0,
              baseYearly: 0,
              perPersonMonthly: 1000,
              perPersonYearly: 10000,
            },
          },
        })
      : route.fallback(),
  );
};

/** Five customers still pay Team's €10; new customers pay €12 since 2 Nov 2026 (6.15, 6.16). */
const oldPriceAnswer = {
  payers: 5,
  yearlyPayers: 2,
  onOldPrice: {
    count: 5,
    oldPrices: {
      baseMonthly: 0,
      baseYearly: 0,
      perPersonMonthly: 1000,
      perPersonYearly: 10000,
    },
    since: "2026-11-02T10:00:00Z",
  },
  movesAt: null,
};

/**
 * An organisation's billing as the Plan tab's frames draw it (4.27 to 4.36): Team, monthly, paid until 1 Dec 2026, last paid on
 * 1 Nov 2026, with its Stripe customer.
 */
const teamBilling = (more: Record<string, unknown> = {}) => ({
  billedBy: "card",
  billingEmail: null,
  customer: {
    id: "cus_Q7xN2kL9aB31",
    dashboardUrl: "https://dashboard.stripe.com/customers/cus_Q7xN2kL9aB31",
  },
  subscription: {
    planId: framePlans.team.id,
    interval: "monthly",
    status: "active",
    currentPeriodEnd: "2026-12-01T00:00:00Z",
    cancelAtPeriodEnd: false,
    scheduledChange: null,
    payment: null,
    priceChange: null,
    lastPaymentAt: "2026-11-01T10:00:00Z",
    ...more,
  },
});
/**
 * The Billing card's frames: Northwind with 11 members, 1 of them a billing manager (4.27), and Team at €10 a person a month.
 * Later routes win, so these answer over the frames' own.
 */
/** A meter of the Usage frames (4.37, 4.42): "ai.requests", whose extra costs €1 per 1,000 when it charges. */
const frameMeter = (
  overage: "charge" | "stop",
  quota: number,
  used: number,
  quotaReachedAt: string | null,
) => ({
  key: "ai.requests",
  quota,
  overage,
  unitPrice: overage === "charge" ? 100 : 0,
  unitSize: overage === "charge" ? 1000 : 1,
  used,
  extra: 0,
  extraAmount: 0,
  quotaReachedAt,
});
/** October, September and August 2026, each as [used, extra, its amount]. */
const framePast = (...months: [number, number, number][]) =>
  months.map(([used, extra, extraAmount], index) => ({
    periodStart: `2026-${String(10 - index).padStart(2, "0")}-01`,
    meters: [{ key: "ai.requests", used, extra, extraAmount }],
  }));
/** The frames' organisations, and GetTenantUsage of one of them in November 2026. */
const usageOfFrame =
  (tenantId: string, usage: Record<string, unknown>) => async (page: Page) => {
    await mockFrameOrganisations(page);
    await page.route(`**/api/v1/platform/tenants/${tenantId}/usage`, (route) =>
      route.fulfill({
        json: {
          periodStart: "2026-11-01",
          periodEnd: "2026-11-30",
          currency: "EUR",
          spendingCap: null,
          ...usage,
        },
      }),
    );
  };
const billingOf =
  (answer: unknown, trials: Record<string, TrialResponse> = {}) =>
  async (page: Page) => {
    await mockFrameOrganisations(page, undefined, { trials });
    await mockFramePlans(page);
    await page.route(
      `**/api/v1/platform/tenants/${frameNorthwind.id}`,
      (route) =>
        route.request().method() === "GET"
          ? route.fulfill({
              json: {
                ...frameNorthwind,
                plan: undefined,
                memberCount: 11,
                freeMemberCount: 1,
              },
            })
          : route.fallback(),
    );
    await page.route(
      `**/api/v1/platform/plans/${framePlans.team.id}`,
      (route) =>
        route.request().method() === "GET"
          ? route.fulfill({
              json: {
                ...framePlans.team,
                prices: { ...framePlans.team.prices, perPersonMonthly: 1000 },
              },
            })
          : route.fallback(),
    );
    await page.route("**/api/v1/platform/tenants/*/billing", (route) =>
      route.fulfill({ json: answer }),
    );
  };
/**
 * The trial frames (4.28 to 4.30, 4.47, 4.48) on 2 Nov 2026: شركة الأفق للتجارة in a Business trial from 22 Oct to 5 Nov 2026, with no card
 * yet, and Northwind after its trial of 15 to 29 Oct 2026. A new organisation's trial is 14 days of Business (GetSignUp).
 */
const ufuq = frameOrganisations.ufuq;
const ufuqPath = pathOf(ufuq);
const trialFrames = {
  [ufuq.id]: {
    plan: { id: framePlans.business.id, name: "Business" },
    startedAt: "2026-10-22T10:00:00Z",
    endsAt: "2026-11-05T10:00:00Z",
    returnsTo: { id: framePlans.free.id, name: "Free" },
  },
  [frameNorthwind.id]: {
    plan: { id: framePlans.business.id, name: "Business" },
    startedAt: "2026-10-15T10:00:00Z",
    endsAt: "2026-10-29T10:00:00Z",
    returnsTo: null,
  },
};
const inTrialBilling = {
  billedBy: "card",
  billingEmail: null,
  customer: {
    id: "cus_R4mT8pQ2vZ06",
    dashboardUrl: "https://dashboard.stripe.com/customers/cus_R4mT8pQ2vZ06",
  },
  subscription: null,
};
const trialOf = (answer: unknown) => async (page: Page) => {
  await page.clock.setFixedTime(new Date("2026-11-02T10:00:00Z"));
  await billingOf(answer, trialFrames)(page);
  await page.route("**/api/v1/public/sign-up", (route) =>
    route.fulfill({
      json: {
        enabled: true,
        tenantLimit: 3,
        trial: {
          plan: { id: framePlans.business.id, name: "Business" },
          days: 14,
        },
      },
    }),
  );
};
const failed = (state: string, more: Record<string, unknown> = {}) => ({
  state,
  failedAt: "2026-11-01T10:00:00Z",
  attempts: 1,
  nextAttemptAt: "2026-11-05T10:00:00Z",
  graceEndsAt: null,
  movedAt: null,
  amountDue: 10000,
  currency: "EUR",
  card: { brand: "visa", last4: "4242" },
  ...more,
});

const shellShots: {
  file: string;
  size: keyof typeof frames;
  language?: Language;
  scheme?: "light" | "dark";
  path?: string;
  mock?: (page: Parameters<typeof mockPlatform>[0]) => Promise<unknown>;
  open?: (page: Parameters<typeof mockPlatform>[0]) => Promise<void>;
  /** The whole page, for a frame taller than the screen. */
  fullPage?: boolean;
  /** What the page shows once it has loaded, when it is not a heading of the content. */
  ready?: (page: Parameters<typeof mockPlatform>[0]) => Promise<void>;
  /** When the page is still: by default no list is busy; a loading frame stays busy, so it waits for nothing more. */
  settled?: (page: Parameters<typeof mockPlatform>[0]) => Promise<void>;
}[] = [
  { file: "2.01-organisations-en-light-desktop", size: "desktop" },
  {
    file: "2.13-organisations-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
  },
  {
    file: "2.01-organisations-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
  },
  { file: "2.01-organisations-en-light-tablet", size: "tablet" },
  {
    file: "2.14-drawer-en-light-phone",
    size: "phone",
    open: (page) => page.getByRole("button", { name: "Open menu" }).click(),
  },
  {
    file: "2.02-command-en-light-desktop",
    size: "desktop",
    open: (page) => page.keyboard.press("ControlOrMeta+k"),
  },
  {
    file: "2.03-account-menu-en-light-desktop",
    size: "desktop",
    open: (page) =>
      page.getByRole("button", { name: "Sam Kelly", exact: true }).click(),
  },
  // The phone's account menu over the drawer (2.27): Profile and Account and security open from it, not from Settings.
  {
    file: "2.27-account-menu-en-light-phone",
    size: "phone",
    open: async (page) => {
      await page.getByRole("button", { name: "Open menu" }).click();
      await page
        .getByRole("dialog")
        .getByRole("button", { name: "Sam Kelly", exact: true })
        .click();
    },
  },
  {
    file: "3.01-notifications-en-light-desktop",
    size: "desktop",
    mock: (page) => mockNotifications(page, bellList),
    open: (page) =>
      page.getByRole("button", { name: /^Notifications/ }).click(),
  },
  {
    file: "3.03-notifications-empty-en-light-desktop",
    size: "desktop",
    mock: (page) => mockNotifications(page, { items: [], unread: 0 }),
    open: (page) =>
      page.getByRole("button", { name: /^Notifications/ }).click(),
  },
  {
    file: "3.02-notifications-all-en-light-desktop",
    size: "desktop",
    mock: (page) => mockNotifications(page, bellList),
    open: async (page) => {
      await page.getByRole("button", { name: /^Notifications/ }).click();
      await bellTabs(page).getByRole("tab", { name: "All" }).click();
      await page.getByRole("link", { name: /was suspended/ }).waitFor();
    },
  },
  // On a phone the bell opens the page of notifications under the top bar (3.08).
  {
    file: "3.08-notifications-en-light-phone",
    size: "phone",
    path: "/notifications",
    mock: (page) => mockNotifications(page, bellList),
    // The top bar names the page; the content has no heading of its own (3.08).
    ready: (page) =>
      page
        .getByRole("main")
        .getByRole("link", { name: /suspended/ })
        .waitFor(),
  },
  {
    file: "3.10-notifications-empty-en-light-phone",
    size: "phone",
    path: "/notifications",
    mock: (page) => mockNotifications(page, { items: [], unread: 0 }),
    ready: (page) =>
      page.getByRole("main").getByText("No notifications").waitFor(),
  },
  {
    file: "2.22-not-found-en-light-desktop",
    size: "desktop",
    path: "/no-such-page",
  },
  {
    file: "2.25-not-found-en-light-phone",
    size: "phone",
    path: "/no-such-page",
  },
  {
    file: "10-settings-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
  },
  {
    file: "2.15-settings-drawer-en-light-phone",
    size: "phone",
    path: "/settings/system-admins",
    open: (page) => page.getByRole("button", { name: "Open menu" }).click(),
  },
  // The theme's card under the Profile form (36-design): System on a light device, Dark chosen in the dark frame. Each shot
  // scrolls the card into view, because the two name fields make the form taller than the frame's.
  {
    file: "8.01-profile-en-light-desktop",
    size: "desktop",
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "8.08-profile-en-light-tablet",
    size: "tablet",
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "8.07-profile-en-light-phone",
    size: "phone",
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "8.09-profile-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: "/settings/profile",
    open: async (page) => {
      await page.getByRole("radio", { name: "Dark" }).click();
      await showTheme(page);
    },
  },
  {
    file: "8.10-profile-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: "/settings/profile",
    open: showTheme,
  },
  {
    file: "8.02-profile-edited-en-light-desktop",
    size: "desktop",
    path: "/settings/profile",
    open: editName,
  },
  {
    file: "8.03-profile-name-empty-en-light-desktop",
    size: "desktop",
    path: "/settings/profile",
    open: emptyName,
  },
  {
    file: "8.04-profile-saved-en-light-desktop",
    size: "desktop",
    path: "/settings/profile",
    mock: (page) => mockRename(page, Language.En),
    open: saveName,
  },
  // Account and security (F62): 9.01, 9.13 and 9.14 show Google connected and two other sessions. The setup's temporary user has
  // one session and no linked account, so Clerk's answers are changed on the way to the page.
  {
    file: "9.01-account-en-light-desktop",
    fullPage: true,
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: otherSessionsShown,
  },
  {
    file: "9.14-account-en-dark-desktop",
    fullPage: true,
    size: "desktop",
    scheme: "dark",
    path: "/settings/account",
    mock: fullState,
    open: otherSessionsShown,
  },
  {
    file: "9.13-account-en-light-phone",
    fullPage: true,
    size: "phone",
    path: "/settings/account",
    mock: fullState,
    open: otherSessionsShown,
  },
  {
    file: "9.04-change-email-en-light-desktop",
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: changeEmail(),
  },
  {
    file: "9.05-email-code-en-light-desktop",
    size: "desktop",
    path: "/settings/account",
    mock: fullState,
    open: changeEmail("deniz.kaya@platform.example"),
  },
  // Plans (6.01 to 6.12, 6.17 to 6.19). The frames draw no dark and no Arabic plan; 6.01 and 6.04 are taken in both too.
  {
    file: "6.01-plans-en-light-desktop",
    size: "desktop",
    path: "/plans",
    mock: mockFramePlans,
  },
  {
    file: "6.01-plans-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: "/plans",
    mock: mockFramePlans,
  },
  {
    file: "6.01-plans-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: "/plans",
    mock: mockFramePlans,
  },
  {
    file: "6.02-plans-loading-en-light-desktop",
    size: "desktop",
    path: "/plans",
    settled: async () => {},
    // ListPlans never answers.
    mock: async (page) => {
      await reduceMotion(page);
      await plansAnswer(() => {})(page);
    },
    ready: (page) =>
      page.getByRole("status", { name: "Loading plans" }).waitFor(),
  },
  {
    file: "6.03-plans-error-en-light-desktop",
    size: "desktop",
    path: "/plans",
    // ListPlans fails; the query tries three times more before the page says so (F8).
    mock: plansAnswer((route) =>
      route.fulfill(problem(500, ErrorCode.ServerError)),
    ),
    ready: (page) =>
      page.getByRole("heading", { name: "Couldn’t load plans" }).waitFor(),
  },
  {
    file: "6.04-plan-team-en-light-desktop",
    size: "desktop",
    path: teamPath,
    mock: mockFramePlans,
  },
  {
    file: "6.04-plan-team-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: teamPath,
    mock: mockFramePlans,
  },
  {
    file: "6.04-plan-team-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: teamPath,
    mock: mockFramePlans,
  },
  {
    file: "6.05-plan-free-default-en-light-desktop",
    size: "desktop",
    path: `/plans/${framePlans.free.id}`,
    mock: mockFramePlans,
  },
  {
    file: "6.06-plan-team-edited-en-light-desktop",
    size: "desktop",
    path: teamPath,
    mock: mockFramePlans,
    open: lowerTheSeats,
  },
  {
    file: "6.07-save-over-a-limit-en-light-desktop",
    size: "desktop",
    path: teamPath,
    mock: teamPreview("over"),
    open: async (page) => {
      await saveLowerSeats(page);
      await page
        .getByRole("alertdialog", { name: "Save changes to Team?" })
        .waitFor();
    },
  },
  {
    file: "6.08-plan-team-saved-en-light-desktop",
    size: "desktop",
    path: teamPath,
    mock: teamPreview("none"),
    open: async (page) => {
      await saveLowerSeats(page);
      await page.getByText("Team saved").waitFor();
    },
  },
  {
    file: "6.09-save-couldnt-check-en-light-desktop",
    size: "desktop",
    path: teamPath,
    mock: teamPreview("fails"),
    open: async (page) => {
      await saveLowerSeats(page);
      await page.getByText("Plan not saved").waitFor();
    },
  },
  {
    file: "6.19-save-shorter-activity-log-en-light-desktop",
    size: "desktop",
    path: teamPath,
    mock: teamShortens,
    open: async (page) => {
      await keepsEntriesField(page).fill("90");
      await page.getByRole("button", { name: "Save changes" }).click();
      await page
        .getByRole("alertdialog", { name: "Save changes to Team?" })
        .waitFor();
    },
  },
  {
    file: "6.10-new-plan-en-light-desktop",
    size: "desktop",
    path: "/plans/new",
    mock: mockFramePlans,
  },
  {
    file: "6.11-new-plan-errors-en-light-desktop",
    size: "desktop",
    path: "/plans/new",
    mock: createAnswers((route) =>
      route.fulfill(problem(409, ErrorCode.PlanNameTaken)),
    ),
    // The API refuses the name; then a period under the floor shows its rule as it is typed, as the frame draws both.
    open: async (page) => {
      await page.getByRole("textbox", { name: "Name" }).fill("Team");
      await seatsField(page).fill("10");
      await keepsEntriesField(page).fill("30");
      await page.getByRole("button", { name: "Create plan" }).click();
      await page.getByText("Another plan has this name.").waitFor();
      await keepsEntriesField(page).fill("7");
      await page.getByText("Enter 30 days or more.").waitFor();
    },
  },
  {
    file: "6.12-plan-created-en-light-desktop",
    size: "desktop",
    path: "/plans/new",
    mock: createAnswers((route) =>
      route.fulfill({
        status: 201,
        json: nonprofit,
        headers: { Location: `/api/v1/platform/plans/${nonprofit.id}` },
      }),
    ),
    open: async (page) => {
      await page.getByRole("textbox", { name: "Name" }).fill("Nonprofit");
      await seatsField(page).fill("10");
      await page.getByRole("button", { name: "Create plan" }).click();
      await page
        .getByRole("heading", { level: 1, name: "Nonprofit" })
        .waitFor();
      await page.getByText("Nonprofit created").waitFor();
    },
  },
  {
    file: "6.17-plans-en-light-phone",
    size: "phone",
    path: "/plans",
    mock: mockFramePlans,
  },
  {
    file: "6.18-plan-team-en-light-phone",
    size: "phone",
    path: teamPath,
    mock: mockFramePlans,
  },
  // A limit whose feature is off (6.21 to 6.25): Team, with Custom roles turned off in the form and its stored 5 disabled.
  ...(
    [
      { file: "6.21-plan-team-custom-roles-off-en-light-desktop" },
      {
        file: "6.22-plan-team-custom-roles-off-en-dark-desktop",
        scheme: "dark" as const,
      },
      {
        file: "6.23-plan-team-custom-roles-off-ar-light-desktop",
        language: Language.Ar,
      },
    ] as const
  ).map((shot) => ({
    ...shot,
    size: "desktop" as const,
    path: teamPath,
    mock: mockFramePlans,
    open: turnCustomRolesOff,
    fullPage: true,
  })),
  // Prices and meters (6.13, 40a): Team priced and metered (teamPricedAndMetered), and the monthly fee per person edited to €12.
  {
    file: "6.13-plan-team-prices-and-meters-en-light-desktop",
    size: "desktop",
    path: teamPath,
    fullPage: true,
    mock: teamPricedAndMetered,
    open: editPerPersonMonthly,
  },
  // A meter that the plan does not set (6.26), and the same meter added to the plan (6.27): 41b.
  {
    file: "6.26-plan-meter-not-on-the-plan-en-light-desktop",
    size: "desktop",
    path: teamPath,
    fullPage: true,
    mock: (page) => teamPricedAndMetered(page, ["ai.requests", "storage"]),
    open: async (page) => {
      await editPerPersonMonthly(page);
      await page.getByRole("button", { name: "Add to plan" }).waitFor();
    },
  },
  {
    file: "6.27-plan-meter-added-to-the-plan-en-light-desktop",
    size: "desktop",
    path: teamPath,
    fullPage: true,
    mock: (page) => teamPricedAndMetered(page, ["ai.requests", "storage"]),
    open: async (page) => {
      await editPerPersonMonthly(page);
      await page.getByRole("button", { name: "Add to plan" }).click();
      await page
        .getByRole("textbox", { name: "storage: quota a month" })
        .waitFor();
      // The new select sits where the button was: move the pointer off it, so the shot shows no hover.
      await page.mouse.move(0, 0);
    },
  },
  // The price change (6.14 to 6.16, 40b): five organisations pay for Team, two of them yearly.
  {
    file: "6.14-price-change-who-pays-en-light-desktop",
    size: "desktop",
    path: teamPath,
    mock: async (page) => {
      await teamAtTen(page);
      await page.route(
        `**/api/v1/platform/plans/${framePlans.team.id}/price-change`,
        (route) =>
          route.fulfill({
            json: {
              payers: 5,
              yearlyPayers: 2,
              onOldPrice: null,
              movesAt: null,
            },
          }),
      );
    },
    open: async (page) => {
      await page
        .getByRole("textbox", { name: "Per person, monthly" })
        .fill("12");
      await page.getByRole("button", { name: "Save changes" }).click();
      await page.getByText("Affects 5 organisations.").waitFor();
    },
  },
  {
    file: "6.15-customers-on-the-old-price-en-light-desktop",
    size: "desktop",
    path: teamPath,
    mock: async (page) => {
      await teamAtTen(page);
      await page.route(
        `**/api/v1/platform/plans/${framePlans.team.id}/price-change`,
        (route) =>
          route.fulfill({
            json: oldPriceAnswer,
          }),
      );
    },
    ready: (page) => page.getByText("5 customers on the old price").waitFor(),
  },
  {
    file: "6.16-move-to-the-new-price-en-light-desktop",
    size: "desktop",
    path: teamPath,
    mock: async (page) => {
      await teamAtTen(page);
      await page.route(
        `**/api/v1/platform/plans/${framePlans.team.id}/price-change`,
        (route) => route.fulfill({ json: oldPriceAnswer }),
      );
    },
    ready: (page) => page.getByText("5 customers on the old price").waitFor(),
    open: async (page) => {
      await page.getByRole("button", { name: "Move to the new price" }).click();
      await page.getByRole("alertdialog").waitFor();
    },
  },
  {
    file: "6.24-plan-team-custom-roles-off-en-light-phone",
    size: "phone",
    path: teamPath,
    fullPage: true,
    mock: mockFramePlans,
    open: async (page) => {
      await page.getByRole("button", { name: "Edit plan" }).click();
      await turnCustomRolesOff(page);
    },
  },
  {
    file: "6.25-plan-team-custom-roles-off-summary-en-light-phone",
    size: "phone",
    path: teamPath,
    mock: teamCustomRolesOff,
  },
  // Activity log (7.01 to 7.08). The frames draw no dark and no Arabic log; 7.01 is taken in both too.
  {
    file: "7.01-activity-en-light-desktop",
    size: "desktop",
    path: "/activity",
    fullPage: true,
    mock: logAnswers(frameLog, () => {}),
  },
  {
    file: "7.01-activity-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: "/activity",
    fullPage: true,
    mock: logAnswers(frameLog, () => {}),
  },
  {
    file: "7.01-activity-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: "/activity",
    fullPage: true,
    mock: logAnswers(frameLog, () => {}),
  },
  {
    file: "7.02-activity-loading-older-en-light-desktop",
    size: "desktop",
    path: "/activity",
    // The older page never comes, so "Loading older activity…" stays.
    mock: async (page) => {
      await reduceMotion(page);
      await logAnswers(frameLog.slice(0, 6), () => {})(page);
    },
    open: async (page) => {
      await loadOlder(page);
      await page.getByText("Loading older activity…").waitFor();
    },
  },
  {
    file: "7.03-activity-loading-en-light-desktop",
    size: "desktop",
    path: "/activity",
    settled: async () => {},
    // The log never answers.
    mock: async (page) => {
      await reduceMotion(page);
      await page.route("**/api/v1/platform/audit-log*", () => {});
    },
    ready: (page) =>
      page.getByRole("status", { name: "Loading activity" }).waitFor(),
  },
  {
    file: "7.04-activity-error-en-light-desktop",
    size: "desktop",
    path: "/activity",
    // The log fails; the query tries three times more before the page says so (F8).
    mock: (page) =>
      page.route("**/api/v1/platform/audit-log*", (route) =>
        route.fulfill(problem(500, ErrorCode.ServerError)),
      ),
    ready: (page) =>
      page.getByRole("heading", { name: "Couldn’t load activity" }).waitFor(),
  },
  {
    file: "7.05-activity-empty-en-light-desktop",
    size: "desktop",
    path: "/activity",
    mock: logAnswers([]),
  },
  {
    file: "7.06-activity-older-failed-en-light-desktop",
    size: "desktop",
    path: "/activity",
    mock: logAnswers(frameLog, (route) =>
      route.fulfill(problem(500, ErrorCode.ServerError)),
    ),
    open: async (page) => {
      await loadOlder(page);
      await page
        .getByText("Couldn’t load older activity")
        .waitFor({ timeout: 30_000 });
    },
    fullPage: true,
  },
  {
    file: "7.07-activity-restarted-en-light-desktop",
    size: "desktop",
    path: "/activity",
    mock: logAnswers(frameLog.slice(0, 6), (route) =>
      route.fulfill(problem(400, ErrorCode.PagingCursorInvalid)),
    ),
    open: async (page) => {
      await loadOlder(page);
      await page.getByText("Starting from the newest activity.").waitFor();
    },
  },
  {
    file: "7.08-activity-en-light-phone",
    size: "phone",
    path: "/activity",
    mock: logAnswers(frameLog.slice(0, 2), () => {}),
  },
  // Organisations (4.01 to 4.26, 4.43, 4.44, 4.46, 4.49) and "Organisation not found" (2.23). The frames draw no dark and no Arabic
  // organisation; 4.01 and 4.09 are taken in both too.
  ...(
    [
      ["4.01-organisations", "/"],
      ["4.09-organisation-overview", northwindPath],
    ] as const
  ).flatMap(([name, path]) =>
    [
      { file: `${name}-en-light-desktop`, path },
      { file: `${name}-en-dark-desktop`, path, scheme: "dark" as const },
      { file: `${name}-ar-light-desktop`, path, language: Language.Ar },
    ].map((shot) => ({
      ...shot,
      size: "desktop" as const,
      mock: (page: Page) => mockFrameOrganisations(page),
      fullPage: name.startsWith("4.09"),
    })),
  ),
  {
    // 4.38: the billing filter, open, with ListTenants' billingCounts while billing is on. "In trial" is 40d's.
    file: "4.38-organisations-billing-filter-en-light-desktop",
    size: "desktop",
    path: "/",
    mock: async (page) => {
      await mockFrameOrganisations(page);
      await page.route("**/api/v1/platform/tenants?*", (route) =>
        route.request().method() === "GET"
          ? route.fulfill({
              json: {
                ...tenantsPage(
                  Object.values(frameOrganisations).map(listItemOf),
                  new URL(route.request().url()).searchParams,
                ),
                billingCounts: { paymentOverdue: 2, contract: 1, inTrial: 0 },
              },
            })
          : route.fallback(),
      );
    },
    open: async (page) => {
      await page.getByRole("button", { name: /^Filter by billing/ }).click();
      await page.getByRole("menu").waitFor();
    },
  },
  {
    // 4.39: the organisations in a trial, chosen in the billing filter.
    file: "4.39-organisations-in-trial-en-light-desktop",
    size: "desktop",
    path: "/?billing=in_trial",
    mock: async (page) => {
      await mockFrameOrganisations(page);
      await page.route("**/api/v1/platform/tenants?*", (route) =>
        route.request().method() === "GET"
          ? route.fulfill({
              json: {
                ...tenantsPage(
                  [listItemOf(frameOrganisations.ufuq)],
                  new URL(route.request().url()).searchParams,
                ),
                billingCounts: { paymentOverdue: 2, contract: 1, inTrial: 1 },
              },
            })
          : route.fallback(),
      );
    },
  },
  {
    file: "4.02-organisations-suspended-en-light-desktop",
    size: "desktop",
    path: "/?status=suspended",
    mock: (page) => mockFrameOrganisations(page),
  },
  {
    file: "4.03-organisations-closed-en-light-desktop",
    size: "desktop",
    path: "/?status=closed",
    mock: (page) => mockFrameOrganisations(page),
  },
  {
    file: "4.04-organisations-no-match-en-light-desktop",
    size: "desktop",
    path: "/?q=harbor",
    mock: (page) => mockFrameOrganisations(page),
  },
  {
    file: "4.05-organisations-loading-en-light-desktop",
    size: "desktop",
    settled: async () => {},
    // ListTenants never answers.
    mock: async (page) => {
      await reduceMotion(page);
      await page.route("**/api/v1/platform/tenants*", () => {});
    },
    ready: (page) =>
      page.getByRole("status", { name: "Loading organisations" }).waitFor(),
  },
  {
    file: "4.06-no-organisations-en-light-desktop",
    size: "desktop",
    mock: (page) => mockFrameOrganisations(page, []),
  },
  {
    file: "4.07-create-organisation-en-light-desktop",
    size: "desktop",
    mock: (page) => mockFrameOrganisations(page),
    open: fillCreate("harbourside-dental"),
  },
  {
    file: "4.08-create-address-taken-en-light-desktop",
    size: "desktop",
    mock: async (page) => {
      await mockFrameOrganisations(page);
      await page.route("**/api/v1/platform/tenants", (route) =>
        route.request().method() === "POST"
          ? route.fulfill(problem(409, ErrorCode.TenantSlugTaken))
          : route.fallback(),
      );
    },
    open: async (page) => {
      await fillCreate("blue-harbour")(page);
      await page
        .getByRole("alertdialog")
        .getByRole("button", { name: "Create organisation" })
        .click();
      await page
        .getByText("This address is taken or reserved. Try another.")
        .waitFor();
    },
  },
  {
    file: "4.10-organisation-menu-en-light-desktop",
    size: "desktop",
    path: northwindPath,
    mock: (page) => mockFrameOrganisations(page),
    open: openMenu("Northwind Traders"),
  },
  {
    file: "4.11-organisation-waiting-for-owner-en-light-desktop",
    size: "desktop",
    path: pathOf(frameOrganisations.harbourside),
    mock: (page) => mockFrameOrganisations(page),
  },
  {
    file: "4.12-invite-owner-en-light-desktop",
    size: "desktop",
    path: pathOf(frameOrganisations.harbourside),
    mock: (page) => mockFrameOrganisations(page),
    open: (page) =>
      page.getByRole("button", { name: "Invite Owner again" }).click(),
  },
  {
    file: "4.13-rename-en-light-desktop",
    size: "desktop",
    path: northwindPath,
    mock: (page) => mockFrameOrganisations(page),
    open: chooseAction("Northwind Traders", "Rename"),
  },
  {
    file: "4.14-organisation-plan-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=plan`,
    mock: (page) => mockFrameOrganisations(page),
  },
  // The Plan tab's Billing card (4.27, 4.31 to 4.36, 40b).
  ...[
    ["4.27-plan-and-billing-card", teamBilling()],
    [
      "4.31-plan-and-billing-payment-retrying",
      teamBilling({ status: "past_due", payment: failed("retrying") }),
    ],
    [
      "4.32-plan-and-billing-grace",
      teamBilling({
        status: "past_due",
        payment: failed("grace", {
          attempts: 4,
          nextAttemptAt: null,
          graceEndsAt: "2026-11-20T10:00:00Z",
        }),
      }),
    ],
    [
      "4.33-plan-and-billing-moved-to-free",
      teamBilling({
        status: "past_due",
        payment: failed("moved_to_default", {
          attempts: 4,
          nextAttemptAt: null,
          graceEndsAt: "2026-11-20T10:00:00Z",
          movedAt: "2026-11-20T10:00:00Z",
        }),
      }),
    ],
    [
      "4.34-plan-and-billing-contract",
      { billedBy: "contract", billingEmail: null, subscription: null },
    ],
    [
      "4.36-plan-and-billing-scheduled-change",
      teamBilling({ cancelAtPeriodEnd: true }),
    ],
  ].map(([name, answer]) => ({
    file: `${name}-en-light-desktop`,
    size: "desktop" as const,
    path: `${northwindPath}?tab=plan`,
    fullPage: true,
    mock: billingOf(answer),
    ready: (page: Page) => page.getByText("Billed by").waitFor(),
  })),
  {
    file: "4.35-switch-to-contract-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=plan`,
    fullPage: true,
    mock: billingOf(teamBilling()),
    ready: (page) => page.getByText("Billed by").waitFor(),
    open: async (page) => {
      await page.getByRole("button", { name: "Switch to Contract" }).click();
      await page.getByRole("alertdialog").waitFor();
    },
  },
  {
    file: "4.45-plan-and-billing-en-light-phone",
    size: "phone",
    path: `${northwindPath}?tab=plan`,
    fullPage: true,
    mock: billingOf(
      teamBilling({ status: "past_due", payment: failed("retrying") }),
    ),
    ready: (page) => page.getByText("Billed by").waitFor(),
  },
  // The trial (4.28 to 4.30, 4.47, 4.48, 40d).
  {
    file: "4.28-plan-and-billing-in-trial-en-light-desktop",
    size: "desktop",
    path: `${ufuqPath}?tab=plan`,
    fullPage: true,
    mock: trialOf(inTrialBilling),
    ready: (page) => page.getByText("Billed by").waitFor(),
  },
  ...(
    [
      ["4.29-give-trial", "desktop", northwindPath, "Give trial"],
      ["4.30-extend-trial", "desktop", ufuqPath, "Extend trial"],
      ["4.47-give-trial", "phone", northwindPath, "Give trial"],
      ["4.48-extend-trial", "phone", ufuqPath, "Extend trial"],
    ] as const
  ).map(([name, size, path, action]) => ({
    file: `${name}-en-light-${size}`,
    size,
    path: `${path}?tab=plan`,
    mock: trialOf(path === ufuqPath ? inTrialBilling : teamBilling()),
    ready: (page: Page) => page.getByText("Billed by").waitFor(),
    open: async (page: Page) => {
      await page.getByRole("button", { name: action }).click();
      const dialog = page.getByRole("alertdialog");
      await dialog.waitFor();
      // 4.30 and 4.48 draw 7 extra days typed.
      if (action === "Extend trial")
        await dialog.getByLabel("Extra days").fill("7");
    },
  })),
  // The Usage tab (4.37, 4.42, 40c): Team gives 10,000 of "ai.requests" and charges the extra; Free gives 100 and stops.
  {
    file: "4.37-organisation-usage-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=usage`,
    mock: usageOfFrame(frameNorthwind.id, {
      planName: "Team",
      meters: [frameMeter("charge", 10000, 7200, null)],
      pastPeriods: framePast([9100, 0, 0], [10600, 600, 60], [8300, 0, 0]),
      spendingCapOffered: true,
    }),
    ready: (page) => page.getByRole("meter").waitFor(),
  },
  {
    file: "4.42-organisation-usage-free-used-up-en-light-desktop",
    size: "desktop",
    path: `${pathOf(frameOrganisations.copperleaf)}?tab=usage`,
    mock: usageOfFrame(frameOrganisations.copperleaf.id, {
      planName: "Free",
      meters: [frameMeter("stop", 100, 100, "2026-11-18T10:00:00Z")],
      pastPeriods: framePast([100, 0, 0], [100, 0, 0], [72, 0, 0]),
      spendingCapOffered: false,
    }),
    ready: (page) => page.getByRole("meter").waitFor(),
  },
  {
    file: "4.15-change-plan-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=plan`,
    mock: async (page) => {
      await mockFrameOrganisations(page);
      await page.route("**/api/v1/platform/tenants/*/plan/preview", (route) =>
        route.fulfill({ json: freePreview }),
      );
    },
    open: async (page) => {
      await page.getByRole("button", { name: "Change plan" }).click();
      const dialog = page.getByRole("alertdialog");
      await dialog.getByRole("combobox", { name: "New plan" }).click();
      await page.getByRole("option", { name: /^Free/ }).click();
      await page.getByRole("listbox").waitFor({ state: "hidden" });
      await dialog.getByText("Here is what changes:").waitFor();
    },
  },
  {
    // No frame draws Change plan before another plan is chosen: the hint under New plan is the reason of the disabled action (F52).
    file: "4.15-change-plan-current-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=plan`,
    mock: (page) => mockFrameOrganisations(page),
    open: async (page) => {
      await page.getByRole("button", { name: "Change plan" }).click();
      await page
        .getByRole("alertdialog")
        .getByText("This is the current plan. Choose another.")
        .waitFor();
    },
  },
  {
    file: "4.16-add-override-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=plan`,
    mock: (page) => mockFrameOrganisations(page),
    open: overrideActivityLog("60", "Data minimisation request"),
  },
  {
    file: "4.49-add-override-longer-until-a-date-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=plan`,
    mock: (page) => mockFrameOrganisations(page),
    open: overrideActivityLog("730", "Legal review"),
  },
  {
    file: "4.17-remove-override-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=plan`,
    mock: (page) =>
      mockFrameOrganisations(page, undefined, { retentionOverride: true }),
    open: (page) =>
      page
        .getByRole("button", {
          name: "Remove the override on Activity log",
        })
        .click(),
  },
  // Custom roles off by an override (4.50 to 4.52): its limit says Off, can only be removed, and cannot be added.
  {
    file: "4.50-organisation-plan-custom-roles-off-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=plan`,
    mock: (page) =>
      mockFrameOrganisations(page, undefined, { customRolesOff: true }),
  },
  {
    file: "4.51-add-override-custom-roles-limit-off-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=plan`,
    mock: (page) =>
      mockFrameOrganisations(page, undefined, {
        customRolesOff: true,
        limitOverride: false,
      }),
    open: async (page) => {
      await page.getByRole("button", { name: "Add override" }).click();
      const dialog = page.getByRole("alertdialog");
      await dialog.getByRole("combobox", { name: "Feature or limit" }).click();
      await page.getByRole("option", { name: "Custom roles limit" }).click();
      await page.getByRole("listbox").waitFor({ state: "hidden" });
      await dialog.getByLabel("Until").fill("2026-12-31");
      await dialog.getByRole("textbox", { name: "Reason" }).fill("Pilot");
    },
  },
  {
    file: "4.52-remove-override-custom-roles-limit-off-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=plan`,
    mock: (page) =>
      mockFrameOrganisations(page, undefined, { customRolesOff: true }),
    open: (page) =>
      page
        .getByRole("button", {
          name: "Remove the override on Custom roles limit",
        })
        .click(),
  },
  {
    file: "4.18-organisation-activity-log-en-light-desktop",
    size: "desktop",
    path: `${northwindPath}?tab=activity`,
    mock: (page) => mockFrameOrganisations(page),
  },
  {
    file: "4.19-suspend-en-light-desktop",
    size: "desktop",
    path: northwindPath,
    mock: (page) => mockFrameOrganisations(page),
    open: async (page) => {
      await chooseAction("Northwind Traders", "Suspend")(page);
      await page
        .getByRole("alertdialog")
        .getByRole("textbox", { name: "Reason" })
        .fill("Unpaid invoices since September");
    },
  },
  {
    file: "4.20-suspended-en-light-desktop",
    size: "desktop",
    path: pathOf(frameOrganisations.blueHarbour),
    mock: (page) => mockFrameOrganisations(page),
  },
  {
    file: "4.21-reactivate-en-light-desktop",
    size: "desktop",
    path: pathOf(frameOrganisations.blueHarbour),
    mock: (page) => mockFrameOrganisations(page),
    open: (page) =>
      page
        .getByRole("status")
        .getByRole("button", { name: "Reactivate" })
        .click(),
  },
  {
    file: "4.22-close-organisation-en-light-desktop",
    size: "desktop",
    path: pathOf(frameOrganisations.fernwood),
    // Fernwood before its closing: active, as the dialog of 4.22 finds it.
    mock: async (page) => {
      await mockFrameOrganisations(page);
      const { plan: _, ...fernwood } = frameOrganisations.fernwood;
      await page.route(`**/api/v1/platform/tenants/${fernwood.id}`, (route) =>
        route.fulfill({
          json: {
            ...fernwood,
            status: "active",
            closedAt: null,
            closedBy: null,
            reopenableUntil: null,
          },
        }),
      );
    },
    open: async (page) => {
      await chooseAction("Fernwood Studio", "Close organisation")(page);
      await page
        .getByRole("alertdialog")
        .getByRole("textbox", { name: "Type fernwood to confirm" })
        .fill("fern");
    },
  },
  {
    file: "4.23-closed-en-light-desktop",
    size: "desktop",
    path: pathOf(frameOrganisations.fernwood),
    mock: (page) => mockFrameOrganisations(page),
  },
  {
    file: "4.24-reopen-en-light-desktop",
    size: "desktop",
    path: pathOf(frameOrganisations.fernwood),
    mock: (page) => mockFrameOrganisations(page),
    open: (page) =>
      page.getByRole("status").getByRole("button", { name: "Reopen" }).click(),
  },
  {
    file: "4.25-being-deleted-en-light-desktop",
    size: "desktop",
    path: pathOf(frameOrganisations.oldMill),
    mock: (page) => mockFrameOrganisations(page),
  },
  {
    file: "4.26-deleted-en-light-desktop",
    size: "desktop",
    path: pathOf(frameOrganisations.greyfield),
    mock: (page) => mockFrameOrganisations(page),
  },
  {
    file: "4.43-organisations-en-light-phone",
    size: "phone",
    fullPage: true,
    mock: (page) => mockFrameOrganisations(page),
    ready: (page) =>
      page.getByRole("list", { name: "Organisations" }).waitFor(),
  },
  {
    file: "4.44-organisation-en-light-phone",
    size: "phone",
    path: northwindPath,
    mock: (page) => mockFrameOrganisations(page),
  },
  {
    file: "4.46-suspend-organisation-en-light-phone",
    size: "phone",
    path: northwindPath,
    mock: (page) => mockFrameOrganisations(page),
    open: async (page) => {
      await chooseAction("Northwind Traders", "Suspend")(page);
      await page
        .getByRole("alertdialog")
        .getByRole("textbox", { name: "Reason" })
        .fill("Unpaid invoices since September");
    },
  },
  {
    file: "2.23-organisation-not-found-en-light-desktop",
    size: "desktop",
    path: "/organisations/0198f0a2-0000-7000-8000-0000000dffff",
    ready: (page) =>
      page.getByRole("heading", { name: "Organisation not found" }).waitFor(),
  },
  // Users (5.01 to 5.19) and System admins (10.01 to 10.16), with the people the frames draw (`frame-users.ts`).
  {
    file: "5.01-users-en-light-desktop",
    size: "desktop",
    path: "/users",
    mock: mockFrameUsers(),
  },
  {
    file: "5.01-users-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: "/users",
    mock: mockFrameUsers(),
  },
  {
    file: "5.01-users-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: "/users",
    mock: mockFrameUsers(),
  },
  {
    file: "5.02-users-suspended-en-light-desktop",
    size: "desktop",
    path: "/users?status=suspended",
    mock: mockFrameUsers(),
  },
  {
    file: "5.03-users-no-match-en-light-desktop",
    size: "desktop",
    path: "/users?q=jonas%40harbour",
    mock: mockFrameUsers(),
  },
  {
    file: "5.04-users-loading-en-light-desktop",
    size: "desktop",
    path: "/users",
    settled: async () => {},
    mock: async (page) => {
      await reduceMotion(page);
      await usersWait(page);
    },
    ready: (page) =>
      page.getByRole("status", { name: "Loading users" }).waitFor(),
  },
  {
    file: "5.05-user-en-light-desktop",
    size: "desktop",
    path: `/users/${frameUsers.ayse.id}`,
    mock: mockFrameUsers(),
  },
  {
    file: "5.05-user-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: `/users/${frameUsers.ayse.id}`,
    mock: mockFrameUsers(),
  },
  {
    file: "5.05-user-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: `/users/${frameUsers.ayse.id}`,
    mock: mockFrameUsers(),
  },
  {
    file: "5.06-user-menu-en-light-desktop",
    size: "desktop",
    path: `/users/${frameUsers.ayse.id}`,
    mock: mockFrameUsers(),
    open: (page) =>
      page.getByRole("button", { name: "Actions for Ayşe Demir" }).click(),
  },
  {
    file: "5.07-user-suspended-en-light-desktop",
    size: "desktop",
    path: `/users/${frameUsers.mark.id}`,
    mock: mockFrameUsers(),
  },
  {
    file: "5.08-user-system-admin-en-light-desktop",
    size: "desktop",
    path: `/users/${frameUsers.deniz.id}`,
    mock: mockFrameUsers(),
  },
  {
    file: "5.09-rename-user-en-light-desktop",
    size: "desktop",
    path: `/users/${frameUsers.ayse.id}`,
    mock: mockFrameUsers(),
    open: userAction("Ayşe Demir", "Rename"),
  },
  {
    file: "5.10-suspend-user-en-light-desktop",
    size: "desktop",
    path: `/users/${frameUsers.ayse.id}`,
    mock: mockFrameUsers(),
    open: async (page) => {
      await userAction("Ayşe Demir", "Suspend")(page);
      await page
        .getByRole("alertdialog")
        .getByRole("textbox", { name: "Reason" })
        .fill("Requested by the organisation");
    },
  },
  {
    file: "5.11-reactivate-user-en-light-desktop",
    size: "desktop",
    path: `/users/${frameUsers.mark.id}`,
    mock: mockFrameUsers(),
    open: (page) =>
      page
        .getByRole("main")
        .getByRole("button", { name: "Reactivate", exact: true })
        .click(),
  },
  {
    file: "5.12-make-owner-en-light-desktop",
    size: "desktop",
    path: `/users/${frameUsers.ayse.id}`,
    mock: mockFrameUsers(),
    open: (page) =>
      page
        .getByRole("button", { name: "Make Owner of شركة الأفق للتجارة" })
        .click(),
  },
  {
    file: "5.13-delete-user-en-light-desktop",
    size: "desktop",
    path: `/users/${frameUsers.mark.id}`,
    mock: mockFrameUsers(),
    open: async (page) => {
      await userAction("Mark Ellis", "Delete user")(page);
      await page
        .getByRole("alertdialog")
        .getByRole("textbox")
        .fill("mark@pacific");
    },
  },
  {
    file: "5.14-cannot-delete-last-owner-en-light-desktop",
    size: "desktop",
    path: `/users/${frameUsers.ayse.id}`,
    mock: mockFrameUsers(),
    open: userAction("Ayşe Demir", "Delete user"),
  },
  {
    file: "5.15-cannot-delete-system-admin-en-light-desktop",
    size: "desktop",
    path: `/users/${frameUsers.deniz.id}`,
    mock: mockFrameUsers(),
    open: userAction("Deniz Kaya", "Delete user"),
  },
  {
    file: "5.16-users-en-light-phone",
    size: "phone",
    path: "/users",
    mock: mockFrameUsers(),
    ready: (page) =>
      page.getByRole("list", { name: "Users" }).first().waitFor(),
  },
  {
    file: "5.17-user-en-light-phone",
    size: "phone",
    path: `/users/${frameUsers.ayse.id}`,
    mock: mockFrameUsers(),
  },
  {
    file: "5.18-users-loading-more-en-light-desktop",
    size: "desktop",
    path: "/users",
    mock: async (page) => {
      await reduceMotion(page);
      await mockFrameUsers({ nextPage: "waits" })(page);
    },
    open: (page) => page.getByRole("button", { name: "Load more" }).click(),
  },
  {
    file: "5.19-users-load-more-failed-en-light-desktop",
    size: "desktop",
    path: "/users",
    mock: mockFrameUsers({ nextPage: "fails" }),
    open: async (page) => {
      await page.getByRole("button", { name: "Load more" }).click();
      await page.getByText("Couldn’t load more users").waitFor();
    },
  },
  {
    file: "10.01-system-admins-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
  },
  {
    file: "10.01-system-admins-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
  },
  {
    file: "10.01-system-admins-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
  },
  {
    file: "10.02-add-system-admin-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
    open: addAdmin("omar@northwind.example"),
  },
  {
    file: "10.03-add-no-user-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
    open: addAdmin("o.haddad@northwind.example"),
  },
  {
    file: "10.04-add-already-an-admin-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
    open: addAdmin("hannah@platform.example"),
  },
  {
    file: "10.05-confirm-its-you-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameGrant("reverify"),
    open: async (page) => {
      await confirmOmar(page);
      await page.getByRole("button", { name: "Make system admin" }).click();
      await page
        .getByRole("alertdialog", { name: "Confirm it’s you" })
        .waitFor();
    },
  },
  {
    file: "10.06-add-confirm-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
    open: confirmOmar,
  },
  {
    file: "10.07-system-admin-added-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameGrant("grant"),
    open: async (page) => {
      await confirmOmar(page);
      await page.getByRole("button", { name: "Make system admin" }).click();
      await page
        .getByRole("cell", { name: "omar@northwind.example" })
        .waitFor();
    },
  },
  {
    file: "10.08-row-menu-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
    open: (page) =>
      page.getByRole("button", { name: "Actions for Hannah Weber" }).click(),
  },
  {
    file: "10.09-your-row-menu-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
    open: (page) =>
      page.getByRole("button", { name: "Actions for Deniz Kaya" }).click(),
  },
  {
    file: "10.10-remove-system-admin-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
    open: removeHannah,
  },
  {
    file: "10.11-system-admin-removed-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameRevoke,
    open: async (page) => {
      await removeHannah(page);
      await page
        .getByRole("alertdialog")
        .getByRole("button", { name: "Remove" })
        .click();
      await page
        .getByText("Hannah Weber is no longer a system admin.")
        .waitFor();
    },
  },
  {
    file: "10.12-system-admins-loading-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    settled: async () => {},
    mock: async (page) => {
      await reduceMotion(page);
      await usersWait(page);
    },
    ready: (page) =>
      page.getByRole("status", { name: "Loading system admins" }).waitFor(),
  },
  {
    file: "10.13-system-admins-error-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: usersFail,
    ready: (page) =>
      page.getByText("Couldn’t load system admins").waitFor({
        timeout: 30_000,
      }),
  },
  {
    file: "10.14-system-admins-en-light-phone",
    size: "phone",
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
    ready: (page) =>
      page.getByRole("list", { name: "System admins" }).first().waitFor(),
  },
  {
    file: "10.15-system-admin-en-light-phone",
    size: "phone",
    path: `/settings/system-admins/${frameAdmins.hannah.id}`,
    mock: mockFrameUsers(),
  },
  {
    file: "10.16-root-label-tooltip-en-light-desktop",
    size: "desktop",
    path: "/settings/system-admins",
    mock: mockFrameUsers(),
    open: async (page) => {
      await page.getByRole("table").getByText("Root", { exact: true }).hover();
      await page.locator('[data-slot="tooltip-content"]').waitFor();
    },
  },
  // Terms of use (11.01 to 11.17), with the versions and people the frames draw (`terms-frames.ts`).
  {
    file: "11.01-terms-en-light-desktop",
    size: "desktop",
    path: "/settings/terms",
    mock: mockFrameTerms(),
  },
  {
    file: "11.16-terms-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: "/settings/terms",
    mock: mockFrameTerms(),
  },
  {
    file: "11.17-terms-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: "/settings/terms",
    mock: mockFrameTerms(),
  },
  {
    file: "11.02-new-version-en-light-desktop",
    size: "desktop",
    path: "/settings/terms/new",
    mock: mockFrameAdd,
    open: async (page) => {
      await page.getByLabel("Applies from").fill(nextYearsDay);
      await page
        .getByRole("textbox", { name: "Link" })
        .fill("https://starter.example/terms/2026-12");
      await page
        .getByRole("textbox", { name: "Summary" })
        .fill(frameTerms.scheduled.summary ?? "");
    },
  },
  {
    file: "11.03-new-version-errors-en-light-desktop",
    size: "desktop",
    path: "/settings/terms/new",
    mock: mockFrameAdd,
    open: async (page) => {
      await page.getByLabel("Applies from").fill("2026-11-01");
      await page
        .getByRole("textbox", { name: "Link" })
        .fill("starter.example/terms");
      await page.getByRole("button", { name: "Add version" }).click();
      await page.getByText("Pick today or a later date.").waitFor();
    },
  },
  {
    file: "11.04-version-added-en-light-desktop",
    size: "desktop",
    path: "/settings/terms/new",
    mock: mockFrameAdd,
    open: async (page) => {
      await page.getByLabel("Applies from").fill(nextYearsDay);
      await page
        .getByRole("textbox", { name: "Link" })
        .fill("https://starter.example/terms/2026-12");
      await page.getByRole("button", { name: "Add version" }).click();
      await page.getByText("Terms of use from 1 Dec 2026 added").waitFor();
      await page.getByRole("cell", { name: "0 of 67 accepted" }).waitFor();
    },
  },
  {
    file: "11.05-version-accepted-en-light-desktop",
    size: "desktop",
    path: `/settings/terms/${frameTerms.scheduled.id}`,
    mock: mockFrameTerms(),
  },
  {
    file: "11.06-version-not-yet-en-light-desktop",
    size: "desktop",
    path: `/settings/terms/${frameTerms.scheduled.id}?people=not_yet`,
    mock: mockFrameTerms(),
  },
  {
    file: "11.07-terms-loading-en-light-desktop",
    size: "desktop",
    path: "/settings/terms",
    settled: async () => {},
    mock: async (page) => {
      await reduceMotion(page);
      await termsWait(page);
    },
    ready: (page) =>
      page.getByRole("status", { name: "Loading the terms of use" }).waitFor(),
  },
  {
    file: "11.08-terms-error-en-light-desktop",
    size: "desktop",
    path: "/settings/terms",
    mock: termsFail,
    ready: (page) =>
      page
        .getByText("Couldn’t load the terms of use")
        .waitFor({ timeout: 30_000 }),
  },
  {
    file: "11.09-terms-nothing-scheduled-en-light-desktop",
    size: "desktop",
    path: "/settings/terms",
    mock: mockFrameAdd,
  },
  {
    file: "11.10-edit-version-en-light-desktop",
    size: "desktop",
    path: `/settings/terms/${frameTerms.scheduled.id}/edit`,
    mock: mockFrameTerms(),
  },
  {
    file: "11.11-cancel-version-en-light-desktop",
    size: "desktop",
    path: `/settings/terms/${frameTerms.scheduled.id}`,
    mock: mockFrameCancel,
    open: async (page) => {
      await page
        .getByRole("button", { name: "Cancel version" })
        .first()
        .click();
      await page.getByRole("alertdialog").waitFor();
    },
  },
  {
    file: "11.12-version-in-force-en-light-desktop",
    size: "desktop",
    path: `/settings/terms/${frameTerms.inForce.id}`,
    mock: mockFrameTerms(),
  },
  {
    file: "11.13-version-cancelled-en-light-desktop",
    size: "desktop",
    path: `/settings/terms/${frameTerms.scheduled.id}`,
    mock: mockFrameCancel,
    open: async (page) => {
      await page
        .getByRole("button", { name: "Cancel version" })
        .first()
        .click();
      await page
        .getByRole("alertdialog")
        .getByRole("button", { name: "Cancel version" })
        .click();
      await page.getByText("Version from 1 Dec 2026 cancelled").waitFor();
      await page.getByRole("link", { name: "Add version" }).waitFor();
    },
  },
  {
    file: "11.14-terms-en-light-phone",
    size: "phone",
    path: "/settings/terms",
    mock: mockFrameTerms(),
    ready: (page) =>
      page.getByRole("list", { name: "Terms of use" }).last().waitFor(),
  },
  {
    file: "11.15-terms-version-en-light-phone",
    size: "phone",
    path: `/settings/terms/${frameTerms.scheduled.id}`,
    mock: mockFrameTerms(),
    ready: (page) => page.getByText("Effective date").waitFor(),
  },
  // Settings › Feature flags (12.01 to 12.33), with the flags the frames draw (`flag-frames.ts`). 12.17 is 12.01 in Staging,
  // which the e2e app is not: every shot shows its environment strip.
  ...(
    [
      ["12.01-flags", "/settings/feature-flags"],
      ["12.02-flags-ready", "/settings/feature-flags?tab=ready_to_remove"],
      ["12.03-flags-unused", "/settings/feature-flags?tab=unused"],
      ["12.04-flags-removed", "/settings/feature-flags?tab=removed"],
      ["12.05-flag", "/settings/feature-flags/roles.bulk_edit"],
      ["12.10-flag-ready", "/settings/feature-flags/activity.export_v2"],
      ["12.11-flag-kill-switch", "/settings/feature-flags/emails.send"],
      ["12.12-flag-removed", "/settings/feature-flags/invitations.csv_import"],
      ["12.18-flag-unused", "/settings/feature-flags/notifications.digest"],
    ] as const
  ).map(([name, path]) => ({
    file: `${name}-en-light-desktop`,
    size: "desktop" as const,
    path,
    mock: mockFrameFlags(),
  })),
  {
    file: "12.16-flags-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: "/settings/feature-flags",
    mock: mockFrameFlags(),
  },
  {
    file: "12.32-flag-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    path: "/settings/feature-flags/roles.bulk_edit",
    mock: mockFrameFlags(),
  },
  {
    file: "12.33-flag-ar-light-desktop",
    size: "desktop",
    language: Language.Ar,
    path: "/settings/feature-flags/roles.bulk_edit",
    mock: mockFrameFlags(),
  },
  {
    file: "12.13-flags-none-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags",
    mock: mockFrameFlags({
      flags: {
        flags: [],
        counts: { all: 0, readyToRemove: 0, unused: 0, removedFromCode: 0 },
      },
    }),
  },
  {
    file: "12.14-flags-loading-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags",
    mock: flagsWait,
    ready: (page) =>
      page.getByRole("status", { name: "Loading the feature flags" }).waitFor(),
    settled: async () => {},
  },
  {
    file: "12.15-flags-error-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags",
    mock: flagsFail,
    ready: (page) =>
      page.getByText("Couldn’t load the feature flags").waitFor(),
  },
  {
    file: "12.29-flag-loading-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/roles.bulk_edit",
    mock: flagsWait,
    ready: (page) => page.getByRole("status", { name: "Loading" }).waitFor(),
    settled: async () => {},
  },
  {
    file: "12.30-flag-error-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/roles.bulk_edit",
    mock: flagsFail,
    ready: (page) => page.getByText("Couldn’t load this flag").waitFor(),
  },
  {
    file: "12.31-flag-not-found-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/nothing.here",
    mock: mockFrameFlags(),
    ready: (page) => page.getByText("Page not found").first().waitFor(),
  },
  {
    file: "12.06-flag-percentage-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/roles.bulk_edit",
    mock: mockFrameFlags(),
    open: async (page) => {
      await page.getByRole("button", { name: "Change", exact: true }).click();
      await page.getByRole("combobox", { name: "Percentage" }).click();
      await page.getByRole("option", { name: "50%" }).click();
      await page.getByRole("listbox").waitFor({ state: "hidden" });
      await page.getByText(/This change affects about/).waitFor();
    },
  },
  {
    file: "12.07-flag-changed-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/roles.bulk_edit",
    mock: async (page) => {
      await mockFrameFlags()(page);
      await page.route(
        "**/api/v1/platform/feature-flags/*/percentage",
        (route) =>
          route.fulfill({
            json: {
              ...frameDetailOf("roles.bulk_edit"),
              flag: {
                ...frameDetailOf("roles.bulk_edit").flag,
                percentage: 50,
              },
            },
          }),
      );
    },
    open: async (page) => {
      await page.getByRole("button", { name: "Change", exact: true }).click();
      await page.getByRole("combobox", { name: "Percentage" }).click();
      await page.getByRole("option", { name: "50%" }).click();
      await page.getByRole("button", { name: "Change to 50%" }).click();
      await page
        .getByText("It is on for 50% of the other organisations.")
        .waitFor();
    },
  },
  {
    file: "12.08-flag-add-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/roles.bulk_edit",
    mock: mockFrameFlags({ previewCount: 1 }),
    open: async (page) => {
      await page.getByRole("button", { name: "Add organisation" }).click();
      await page.getByRole("combobox", { name: "Organisation" }).click();
      await page.getByRole("option").first().click();
      await page.getByRole("listbox").waitFor({ state: "hidden" });
      await page.getByText("This change affects 1 organisation.").waitFor();
    },
  },
  {
    file: "12.09-flag-remove-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/roles.bulk_edit",
    mock: mockFrameFlags(),
    open: (page) =>
      page.getByRole("button", { name: "Remove Northwind Traders" }).click(),
  },
  {
    file: "12.24-flag-turn-on-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/roles.bulk_edit",
    mock: mockFrameFlags({ previewCount: 8 }),
    open: async (page) => {
      await page.getByRole("button", { name: "Turn on for everyone" }).click();
      await page.getByText(/more organisations get it now/).waitFor();
    },
  },
  {
    file: "12.25-flag-turn-off-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/activity.export_v2",
    mock: mockFrameFlags({ previewCount: 8 }),
    open: async (page) => {
      await page.getByRole("button", { name: "Turn off for everyone" }).click();
      await page.getByText(/organisations lose it now/).waitFor();
    },
  },
  {
    file: "12.26-kill-switch-turn-off-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/emails.send",
    mock: mockFrameFlags({ previewCount: 10 }),
    open: async (page) => {
      await page.getByRole("button", { name: "Turn off for everyone" }).click();
      await page.getByText(/until you turn it back on/).waitFor();
    },
  },
  {
    file: "12.27-kill-switch-off-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/emails.send",
    mock: mockFrameFlags({ details: { "emails.send": emailsOff } }),
  },
  {
    file: "12.28-kill-switch-turn-on-en-light-desktop",
    size: "desktop",
    path: "/settings/feature-flags/emails.send",
    mock: mockFrameFlags({
      details: { "emails.send": emailsOff },
      previewCount: 10,
    }),
    open: async (page) => {
      await page.getByRole("button", { name: "Turn on for everyone" }).click();
      await page.getByText(/get it again/).waitFor();
    },
  },
  {
    file: "12.19-flags-en-light-phone",
    size: "phone",
    path: "/settings/feature-flags",
    mock: mockFrameFlags(),
    ready: (page) =>
      page.getByRole("list", { name: "Feature flags" }).last().waitFor(),
  },
  {
    file: "12.20-flag-en-light-phone",
    size: "phone",
    path: "/settings/feature-flags/roles.bulk_edit",
    mock: mockFrameFlags(),
  },
  {
    file: "12.21-flag-percentage-en-light-phone",
    size: "phone",
    path: "/settings/feature-flags/roles.bulk_edit",
    mock: mockFrameFlags(),
    open: async (page) => {
      await page.getByRole("button", { name: "Change", exact: true }).click();
      await page.getByRole("combobox", { name: "Percentage" }).click();
      await page.getByRole("option", { name: "25%" }).click();
      await page.getByRole("listbox").waitFor({ state: "hidden" });
      await page.getByText(/This change affects about/).waitFor();
    },
  },
  {
    file: "12.22-kill-switch-turn-off-en-light-phone",
    size: "phone",
    path: "/settings/feature-flags/emails.send",
    mock: mockFrameFlags({ previewCount: 10 }),
    open: async (page) => {
      await page.getByRole("button", { name: "Turn off for everyone" }).click();
      await page.getByText(/until you turn it back on/).waitFor();
    },
  },
  {
    file: "12.23-kill-switch-off-en-light-phone",
    size: "phone",
    path: "/settings/feature-flags/emails.send",
    mock: mockFrameFlags({ details: { "emails.send": emailsOff } }),
  },
  {
    file: "7.10-activity-flags-en-light-desktop",
    size: "desktop",
    path: "/activity",
    mock: logAnswers(flagsLog),
  },
  {
    file: "3.07-notifications-flags-en-light-desktop",
    size: "desktop",
    mock: (page) => mockNotifications(page, flagsBell),
    open: (page) =>
      page.getByRole("button", { name: /^Notifications/ }).click(),
  },
  {
    file: "7.09-activity-terms-en-light-desktop",
    size: "desktop",
    path: "/activity",
    mock: logAnswers(termsLog),
  },
];

/** No list of the page is busy. */
const notBusy = (page: Page) =>
  expect(page.locator('[aria-busy="true"]')).toHaveCount(0);

/** A shot's language, theme and page when it names none: English, light, the start page. */
const withDefaults = (shot: (typeof shellShots)[number]) => ({
  ...shot,
  language: shot.language ?? Language.En,
  scheme: shot.scheme ?? "light",
  path: shot.path ?? "/",
});

for (const shot of shellShots.map(withDefaults))
  test(`Screenshot_Shell_${shot.file}`, async ({ page, context, baseURL }) => {
    await setupClerkTestingToken({ page });
    await page.emulateMedia({ colorScheme: shot.scheme });
    await page.setViewportSize(frames[shot.size]);
    await context.addCookies([
      { name: localeCookie, value: shot.language, url: baseURL },
    ]);
    await mockPlatform(page, shot.language);
    await shot.mock?.(page);

    await page.goto(shot.path);
    await (shot.ready?.(page) ??
      page.getByRole("main").getByRole("heading").first().waitFor());
    await (shot.settled ?? notBusy)(page);
    await shot.open?.(page);
    // The screen as it rests: every opening animation has finished. A popup mounts a frame or two after the click, so its
    // animation starts only then.
    await page.evaluate(
      () =>
        new Promise((done) =>
          requestAnimationFrame(() => requestAnimationFrame(done)),
        ),
    );
    // An endless animation, such as a skeleton's pulse, has no end to wait for.
    await page.evaluate(() =>
      Promise.all(
        document
          .getAnimations()
          .filter(
            (each) =>
              each.effect?.getTiming().iterations !== Number.POSITIVE_INFINITY,
          )
          .map((each) => each.finished),
      ),
    );

    await page.screenshot({
      path: `${folder}/shell-${shot.file}.png`,
      fullPage: shot.fullPage,
    });
  });

// The platform's Entry frames (1.01 to 1.12). The sign-in shots run signed out with the setup's user, who has an authenticator
// app, so Clerk asks for the second factor on its own. The enrolment shots sign in a user of their own, made for the shot and
// deleted after it, because only a user without an authenticator app reaches the enrolment. Playwright changes one answer of
// Clerk: the locked account of 1.07, which a real lock would keep for the setup's user for the rest of the run.
const toTheCode = async (page: Page) => {
  await page.getByRole("textbox", { name: "Email" }).fill(enrolledUser().email);
  await page.getByRole("button", { name: "Continue", exact: true }).click();
  await page.getByRole("heading", { name: "Check your email" }).waitFor();
};

/** The address the frames show, typed in the focused field. */
const typeTheFramesAddress = (page: Page) =>
  page.getByRole("textbox", { name: "Email" }).fill("deniz@platform.example");

const toTheSecondFactor = async (page: Page) => {
  await toTheCode(page);
  await page.getByRole("textbox", { name: "Code" }).pressSequentially("424242");
  await page.getByRole("button", { name: "Sign in" }).click();
  await page
    .getByRole("heading", { name: "Enter the code from your app" })
    .waitFor();
};

const entryShots: {
  file: string;
  size: keyof typeof frames;
  scheme?: "light" | "dark";
  path?: string;
  mock?: (page: Page) => Promise<unknown>;
  open?: (page: Page) => Promise<unknown>;
}[] = [
  {
    file: "1.01-sign-in-en-light-desktop",
    size: "desktop",
    open: typeTheFramesAddress,
  },
  {
    file: "1.02-check-your-email-en-light-desktop",
    size: "desktop",
    open: toTheCode,
  },
  {
    file: "1.05-authenticator-code-en-light-desktop",
    size: "desktop",
    open: toTheSecondFactor,
  },
  {
    file: "1.06-backup-code-en-light-desktop",
    size: "desktop",
    open: async (page) => {
      await toTheSecondFactor(page);
      await page
        .getByRole("button", { name: "Use a backup code instead" })
        .click();
      await page
        .getByRole("textbox", { name: "Backup code" })
        .fill("4f7k-2m9q");
    },
  },
  {
    file: "1.07-wrong-code-locked-en-light-desktop",
    size: "desktop",
    mock: (page) =>
      page.route("**/v1/client/sign_ins/*/attempt_first_factor**", (route) =>
        route.fulfill({
          status: 403,
          json: {
            errors: [{ code: "user_locked", message: "", long_message: "" }],
          },
        }),
      ),
    open: async (page) => {
      await toTheCode(page);
      await page
        .getByRole("textbox", { name: "Code" })
        .pressSequentially("381946");
      await page.getByRole("button", { name: "Sign in" }).click();
      await page.getByRole("alert").filter({ hasText: "Too many" }).waitFor();
    },
  },
  {
    file: "1.09-sign-in-en-dark-desktop",
    size: "desktop",
    scheme: "dark",
    open: typeTheFramesAddress,
  },
  {
    file: "1.10-phone-sign-in-en-light-phone",
    size: "phone",
    open: typeTheFramesAddress,
  },
  {
    file: "1.11-phone-check-your-email-en-light-phone",
    size: "phone",
    open: toTheCode,
  },
  {
    file: "1.12-phone-two-step-verification-en-light-phone",
    size: "phone",
    open: toTheSecondFactor,
  },
  // Without Clerk's script the callback never ends, so the card stays as it shows while Clerk works.
  {
    file: "1.13-signing-you-in-en-light-desktop",
    size: "desktop",
    path: "/sso-callback",
    mock: (page) => page.route("**/clerk.browser.js", (route) => route.abort()),
  },
  // The same card after 10 seconds: Playwright's clock moves the page's time on.
  {
    file: "1.14-signing-you-in-taking-longer-en-light-desktop",
    size: "desktop",
    path: "/sso-callback",
    mock: async (page) => {
      await page.clock.install();
      await page.route("**/clerk.browser.js", (route) => route.abort());
    },
    open: async (page) => {
      await page.clock.fastForward(10_000);
      await page.getByRole("link", { name: "Try again" }).waitFor();
    },
  },
];

/** The screen at rest: no pointer over a control, and every opening animation has finished. */
const still = async (page: Page) => {
  await page.mouse.move(0, 0);
  await page.evaluate(
    () =>
      new Promise((done) =>
        requestAnimationFrame(() => requestAnimationFrame(done)),
      ),
  );
  await page.evaluate(() =>
    Promise.all(document.getAnimations().map((each) => each.finished)),
  );
};

test.describe("signed out", () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  for (const shot of entryShots.map((each) => ({
    ...each,
    path: each.path ?? "/sign-in",
  })))
    test(`Screenshot_Entry_${shot.file}`, async ({
      page,
      context,
      baseURL,
    }) => {
      await setupClerkTestingToken({ page });
      await page.emulateMedia({ colorScheme: shot.scheme ?? "light" });
      await page.setViewportSize(frames[shot.size]);
      await context.addCookies([
        { name: localeCookie, value: Language.En, url: baseURL },
      ]);
      await shot.mock?.(page);

      await page.goto(shot.path);
      await page.getByRole("heading", { level: 1 }).waitFor();
      await shot.open?.(page);
      await still(page);

      await page.screenshot({ path: `${folder}/entry-${shot.file}.png` });
    });

  // Frames 1.03 and 1.04: a user without an authenticator app signs in, and the gate asks them to set one up.
  test("Screenshot_Entry_1.03-and-1.04-enrolment-en-light-desktop", async ({
    page,
  }) => {
    test.setTimeout(120_000);
    const email = `e2e-platform-${randomUUID()}+clerk_test@example.com`;
    const id = await createUser(email);
    try {
      await setupClerkTestingToken({ page });
      await page.emulateMedia({ colorScheme: "light" });
      await page.setViewportSize(frames.desktop);
      await page.goto("/sign-in");
      await signInWithEmailCode(page, email);
      await page
        .getByRole("heading", { name: "Set up two-step verification" })
        .waitFor({ timeout: 30_000 });
      const key = page.getByText(/^[A-Z2-7]{4}( [A-Z2-7]{1,4})+$/);
      await still(page);
      await page.screenshot({
        path: `${folder}/entry-1.03-set-up-the-authenticator-en-light-desktop.png`,
      });

      // biome-ignore lint/performance/useDomNodeTextContent: Playwright's Locator.innerText reads the key as the page shows it; textContent can be null.
      const secret = (await key.innerText()).replaceAll(" ", "");
      await page
        .getByRole("textbox", { name: "Enter the 6-digit code the app shows" })
        .pressSequentially(await totpCode(secret));
      await page
        .getByRole("button", { name: "Turn on two-step sign-in" })
        .click();
      await expect(
        page.getByRole("list", { name: "Backup codes" }).getByRole("listitem"),
      ).toHaveCount(10);
      await still(page);
      await page.screenshot({
        path: `${folder}/entry-1.04-save-backup-codes-en-light-desktop.png`,
      });
    } finally {
      await deleteUser(id);
    }
  });
});

// Frame 1.08: a signed-in person whom the door refuses.
test("Screenshot_Entry_1.08-not-a-system-admin-en-light-desktop", async ({
  page,
}) => {
  await setupClerkTestingToken({ page });
  await page.emulateMedia({ colorScheme: "light" });
  await page.setViewportSize(frames.desktop);
  await page.route("**/api/v1/me", (route) =>
    route.fulfill({
      json: {
        id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e31",
        email: "maria@northwind.example",
        name: "Maria Rossi",
        isSystemAdmin: false,
        language: Language.En,
        timeZone: "Europe/Dublin",
      },
    }),
  );
  await page.route("**/api/v1/platform/tenants**", (route) =>
    route.fulfill({
      status: 403,
      json: { status: 403, title: "Forbidden", code: ErrorCode.AuthForbidden },
    }),
  );

  await page.goto("/");
  await page
    .getByRole("heading", { name: "You don’t have access to the platform" })
    .waitFor();
  // The address comes from Clerk's user, which loads after the page.
  await page.locator("bdi").filter({ hasText: "@" }).waitFor();
  await still(page);

  await page.screenshot({
    path: `${folder}/entry-1.08-not-a-system-admin-en-light-desktop.png`,
  });
});
