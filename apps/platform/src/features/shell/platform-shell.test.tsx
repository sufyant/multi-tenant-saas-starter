import { ErrorCode, Language, UserResponseStatus } from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
  getGetUserMockHandler,
  getGetUserResponseMock,
  getListTenantsMockHandler,
  getListTenantsResponseMock,
} from "@repo/api-client/msw";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import { renderWithProviders } from "../../../test/render";
import { PlatformShell } from "./platform-shell";

const navigation = vi.hoisted(() => ({ pathname: "/" }));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
  usePathname: () => navigation.pathname,
}));
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({ signOut: vi.fn() }),
  useUser: () => ({
    user: { primaryEmailAddress: { emailAddress: "ali.kaya@gmail.com" } },
  }),
}));
vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

const notAUser = {
  status: 404,
  title: "Not Found",
  code: ErrorCode.UserNotFound,
  traceId: "00-trace-01",
};

const refused = {
  status: 403,
  title: "Forbidden",
  code: ErrorCode.AuthForbidden,
  traceId: "00-trace-02",
};

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  navigation.pathname = "/";
});
afterAll(() => server.close());

describe("PlatformShell", () => {
  it("PlatformShell_MainMenu_ShowsThePlatformsBell", async () => {
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
      http.get("*/v1/platform/notifications/unread-count", () =>
        HttpResponse.json({ unread: 5 }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    expect(
      await within(
        screen.getByRole("navigation", { name: "Main menu" }),
      ).findByRole("button", { name: "Notifications, 5 unread" }),
    ).toBeInTheDocument();
  });

  it("PlatformShell_SystemAdmin_StartsTheMenuWithTheProductAndPlatform", async () => {
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    expect(
      await screen.findByRole("navigation", { name: "Main menu" }),
    ).toHaveTextContent(/^SStarterPlatform/);
  });

  it("PlatformShell_MainMenu_ShowsOnlyThePagesThatExist", async () => {
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    const rail = within(
      await screen.findByRole("navigation", { name: "Main menu" }),
    );
    expect(rail.getByRole("link", { name: "Settings" })).toHaveAttribute(
      "href",
      "/settings/profile",
    );
    expect(rail.getByRole("link", { name: "Users" })).toHaveAttribute(
      "href",
      "/users",
    );
    expect(rail.getByRole("link", { name: "Plans" })).toHaveAttribute(
      "href",
      "/plans",
    );
    expect(rail.getByRole("link", { name: "Activity log" })).toHaveAttribute(
      "href",
      "/activity",
    );
    expect(rail.getByRole("link", { name: "Organisations" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("PlatformShell_MainMenu_IsOrganisationsUsersPlansActivityLogThenSettings", async () => {
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    const rail = within(
      await screen.findByRole("navigation", { name: "Main menu" }),
    );
    expect(
      rail.getAllByRole("link").map((link) => link.getAttribute("href")),
    ).toEqual(["/", "/users", "/plans", "/activity", "/settings/profile"]);
  });

  it("PlatformShell_ActivityPage_MarksActivityLogCurrent", async () => {
    navigation.pathname = "/activity";
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    const rail = within(
      await screen.findByRole("navigation", { name: "Main menu" }),
    );
    expect(rail.getByRole("link", { name: "Activity log" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    const crumbs = within(
      screen.getByRole("navigation", { name: "Breadcrumb" }),
    );
    expect(crumbs.getByText("Activity log")).toBeInTheDocument();
  });

  it("PlatformShell_UsersPage_MarksUsersCurrent", async () => {
    navigation.pathname = "/users";
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    const rail = within(
      await screen.findByRole("navigation", { name: "Main menu" }),
    );
    expect(rail.getByRole("link", { name: "Users" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    const crumbs = within(
      screen.getByRole("navigation", { name: "Breadcrumb" }),
    );
    expect(crumbs.getByText("Users")).toBeInTheDocument();
  });

  it("PlatformShell_NotFound_NamesThePageAndMarksNoRow", async () => {
    navigation.pathname = "/an-old-link";
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
    );

    renderWithProviders(<PlatformShell notFound>Content</PlatformShell>);

    const rail = within(
      await screen.findByRole("navigation", { name: "Main menu" }),
    );
    expect(
      rail.getByRole("link", { name: "Organisations" }),
    ).not.toHaveAttribute("aria-current");
    const crumbs = within(
      screen.getByRole("navigation", { name: "Breadcrumb" }),
    ).getAllByRole("listitem");
    expect(crumbs.map((crumb) => crumb.textContent)).toEqual([
      "Platform",
      "Page not found",
    ]);
  });

  // The phone's bell opens this page (3.08 of the platform); the top bar and the breadcrumb name it.
  it("PlatformShell_NotificationsPage_NamesThePageAndMarksNoRow", async () => {
    navigation.pathname = "/notifications";
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    const rail = within(
      await screen.findByRole("navigation", { name: "Main menu" }),
    );
    expect(
      rail.getByRole("link", { name: "Organisations" }),
    ).not.toHaveAttribute("aria-current");
    const crumbs = within(
      screen.getByRole("navigation", { name: "Breadcrumb" }),
    ).getAllByRole("listitem");
    expect(crumbs.map((crumb) => crumb.textContent)).toEqual([
      "Platform",
      "Notifications",
    ]);
  });

  it("PlatformShell_SystemAdminsPage_ShowsTheSettingsMenuAndTheBreadcrumb", async () => {
    navigation.pathname = "/settings/system-admins";
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    const crumbs = within(
      await screen.findByRole("navigation", { name: "Breadcrumb" }),
    );
    expect(crumbs.getByText("Settings")).toBeInTheDocument();
    expect(crumbs.getByText("System admins")).toBeInTheDocument();
    const menu = within(
      screen.getByRole("navigation", { name: "Settings menu" }),
    );
    expect(menu.getAllByRole("link").map((link) => link.textContent)).toEqual([
      "Back to the platform",
      "Profile",
      "Account and security",
      "System admins",
    ]);
  });

  // The platform's own item of the account menu (2.03): the web app of WEB_APP_URL, which the server layout gives the shell.
  it("PlatformShell_AccountMenu_OpensTheWebAppOfTheFrame", async () => {
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({
          isSystemAdmin: true,
          name: "Deniz Kaya",
          language: "en",
        }),
      ),
    );

    renderWithProviders(
      <PlatformShell webAppUrl="https://app.example.test">
        Content
      </PlatformShell>,
    );
    const [accountMenu] = await screen.findAllByRole("button", {
      name: /Deniz Kaya/,
    });
    if (!accountMenu) throw new Error("No account menu.");
    await userEvent.click(accountMenu);

    expect(
      await screen.findByRole("menuitem", {
        name: "Open the web app (opens in a new tab)",
      }),
    ).toHaveAttribute("href", "https://app.example.test");
  });

  it("PlatformShell_UserPage_NamesTheUserInTheBreadcrumb", async () => {
    const userId = "0198f0a2-0000-7000-8000-0000000000a1";
    navigation.pathname = `/users/${userId}`;
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
      http.get(`*/v1/platform/users/${userId}`, () =>
        HttpResponse.json({
          id: userId,
          email: "ayse@northwind.example",
          name: "Ayşe Yılmaz",
          status: UserResponseStatus.Active,
          isSystemAdmin: false,
          isRootSystemAdmin: false,
          systemAdminGrantedAt: null,
          membershipCount: 0,
          createdAt: "2026-03-03T09:20:00Z",
          suspendedAt: null,
          suspendedBy: null,
          memberships: [],
        }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    const crumbs = within(
      await screen.findByRole("navigation", { name: "Breadcrumb" }),
    );
    expect(await crumbs.findByText("Ayşe Yılmaz")).toBeInTheDocument();
    expect(crumbs.getByText("Users")).toBeInTheDocument();
  });

  it("PlatformShell_DeletedUsersPage_SaysDeletedUserInTheBreadcrumb", async () => {
    const userId = "0198f0a2-0000-7000-8000-0000000000a6";
    navigation.pathname = `/users/${userId}`;
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: Language.En }),
      ),
      getGetUserMockHandler(
        getGetUserResponseMock({
          id: userId,
          name: null,
          email: null,
          status: UserResponseStatus.Deleted,
        }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    const crumbs = within(
      await screen.findByRole("navigation", { name: "Breadcrumb" }),
    );
    expect(await crumbs.findByText("Deleted user")).toBeInTheDocument();
  });

  it("PlatformShell_ActiveUserWithoutNames_NamesThemByTheirEmailInTheBreadcrumb", async () => {
    const userId = "0198f0a2-0000-7000-8000-0000000000a8";
    navigation.pathname = `/users/${userId}`;
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: Language.En }),
      ),
      getGetUserMockHandler(
        getGetUserResponseMock({
          id: userId,
          name: null,
          email: "root@starter.example",
          status: UserResponseStatus.Active,
        }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    const crumbs = within(
      await screen.findByRole("navigation", { name: "Breadcrumb" }),
    );
    expect(await crumbs.findByText("root@starter.example")).toBeInTheDocument();
    expect(crumbs.queryByText("Deleted user")).toBeNull();
  });

  it("PlatformShell_OrganisationPage_NamesTheOrganisationInTheBreadcrumb", async () => {
    const tenantId = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f";
    navigation.pathname = `/organisations/${tenantId}`;
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
      http.get(`*/v1/platform/tenants/${tenantId}`, () =>
        HttpResponse.json({
          id: tenantId,
          name: "Kestrel Labs",
          slug: "kestrel",
          status: "active",
          suspensionReason: null,
          closedAt: null,
          purgedAt: null,
          reopenableUntil: null,
          purgeWarningAt: null,
          memberCount: 4,
          ownerCount: 1,
          owners: [],
          ownerInvitation: null,
        }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    const crumbs = within(
      await screen.findByRole("navigation", { name: "Breadcrumb" }),
    );
    expect(await crumbs.findByText("Kestrel Labs")).toBeInTheDocument();
    expect(crumbs.getByText("Organisations")).toBeInTheDocument();
  });

  it("PlatformShell_SystemAdminFromTheStart_DoesNotAskTheDoor", async () => {
    const doorCalls = vi.fn();
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
      getListTenantsMockHandler(() => {
        doorCalls();
        return getListTenantsResponseMock();
      }),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    expect(await screen.findByText("Content")).toBeInTheDocument();
    expect(doorCalls).not.toHaveBeenCalled();
  });

  it("PlatformShell_NoSystemAdminAndTheDoorRefuses_SaysYouDontHaveAccessAfterOneDoorCall", async () => {
    const consoleError = vi.spyOn(console, "error");
    const meCalls = vi.fn();
    const doorCalls = vi.fn();
    server.use(
      getGetMeMockHandler(() => {
        meCalls();
        return getGetMeResponseMock({ isSystemAdmin: false, language: "en" });
      }),
      http.get("*/v1/platform/tenants", () => {
        doorCalls();
        return HttpResponse.json(refused, { status: 403 });
      }),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    expect(
      await screen.findByRole("heading", { name: "You don’t have access" }),
    ).toBeInTheDocument();
    expect(screen.getByText("ali.kaya@gmail.com")).toHaveAttribute(
      "dir",
      "ltr",
    );
    expect(screen.queryByText("Content")).not.toBeInTheDocument();
    expect(doorCalls).toHaveBeenCalledOnce();
    expect(meCalls).toHaveBeenCalledOnce();
    expect(consoleError).not.toHaveBeenCalled();
    consoleError.mockRestore();
  });

  it("PlatformShell_RootSystemAdminAlreadyAUser_AsksTheDoorThenReadsGetMeAgain", async () => {
    // A root system admin who is already a user, for example a member of a tenant, has no grant until the door writes it
    // (SystemAdminDirectory), so GetMe answers isSystemAdmin false until then.
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: false, language: "en" }),
        { once: true },
      ),
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
      getListTenantsMockHandler(),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    expect(await screen.findByText("Content")).toBeInTheDocument();
  });

  it("PlatformShell_DoorLetsInButGetMeStillNoSystemAdmin_SaysYouDontHaveAccessAfterOneDoorCall", async () => {
    const meCalls = vi.fn();
    const doorCalls = vi.fn();
    server.use(
      getGetMeMockHandler(() => {
        meCalls();
        return getGetMeResponseMock({ isSystemAdmin: false, language: "en" });
      }),
      getListTenantsMockHandler(() => {
        doorCalls();
        return getListTenantsResponseMock();
      }),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    expect(
      await screen.findByRole("heading", { name: "You don’t have access" }),
    ).toBeInTheDocument();
    expect(doorCalls).toHaveBeenCalledOnce();
    expect(meCalls).toHaveBeenCalledTimes(2);
  });

  it("PlatformShell_RootSystemAdminNotAUserYet_AsksTheDoorThenReadsGetMeAgain", async () => {
    // The door writes the root system admin's user at their first platform request (SystemAdminDirectory), so GetMe answers
    // 404 until then.
    server.use(
      http.get("*/v1/me", () => HttpResponse.json(notAUser, { status: 404 }), {
        once: true,
      }),
      getGetMeMockHandler(
        getGetMeResponseMock({ isSystemAdmin: true, language: "en" }),
      ),
      getListTenantsMockHandler(),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    expect(await screen.findByText("Content")).toBeInTheDocument();
  });

  it("PlatformShell_DoorLetsInButGetMeStill404_SaysYouDontHaveAccess", async () => {
    server.use(
      http.get("*/v1/me", () => HttpResponse.json(notAUser, { status: 404 })),
      getListTenantsMockHandler(),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    expect(
      await screen.findByRole("heading", { name: "You don’t have access" }),
    ).toBeInTheDocument();
  });

  it("PlatformShell_NotAUserAndTheDoorRefuses_SaysYouDontHaveAccess", async () => {
    server.use(
      http.get("*/v1/me", () => HttpResponse.json(notAUser, { status: 404 })),
      http.get("*/v1/platform/tenants", () =>
        HttpResponse.json(refused, { status: 403 }),
      ),
    );

    renderWithProviders(<PlatformShell>Content</PlatformShell>);

    expect(
      await screen.findByRole("heading", { name: "You don’t have access" }),
    ).toBeInTheDocument();
  });

  describe("while the first reads come", () => {
    // A read that waits holds until its test ends, then fails: an answer that never comes would stay open and hold up the
    // reads of the next tests.
    let releases: (() => void)[] = [];
    afterEach(() => {
      for (const release of releases) release();
      releases = [];
    });
    const held = async () => {
      await new Promise<void>((resolve) => releases.push(resolve));
      return HttpResponse.error();
    };
    const meWaits = () => http.get("*/v1/me", held);

    it("PlatformShell_GetMeWaits_ShowsTheMenuAndTheBreadcrumbAtOnce", () => {
      navigation.pathname = "/users";
      server.use(meWaits());

      renderWithProviders(<PlatformShell>Content</PlatformShell>);

      const rail = screen.getByRole("navigation", { name: "Main menu" });
      expect(within(rail).getByRole("link", { name: "Users" })).toHaveAttribute(
        "href",
        "/users",
      );
      expect(
        screen.getByRole("navigation", { name: "Breadcrumb" }),
      ).toHaveTextContent("Users");
      expect(screen.queryByText("Content")).not.toBeInTheDocument();
    });

    it("PlatformShell_GetMeWaits_ShowsNoSkeletonBeforeTheDelayAndTheSkeletonAfterIt", async () => {
      server.use(meWaits());

      renderWithProviders(<PlatformShell>Content</PlatformShell>);

      expect(screen.queryByRole("status")).not.toBeInTheDocument();
      expect(
        await screen.findByRole("status", { name: "Loading" }),
      ).toBeInTheDocument();
      expect(screen.getByRole("main")).toContainElement(
        screen.getByRole("status", { name: "Loading" }),
      );
    });

    it("PlatformShell_DoorWaits_KeepsTheShellAndShowsNoContent", async () => {
      server.use(
        getGetMeMockHandler(
          getGetMeResponseMock({ isSystemAdmin: false, language: "en" }),
        ),
        http.get("*/v1/platform/tenants", held),
      );

      renderWithProviders(<PlatformShell>Content</PlatformShell>);

      expect(
        await screen.findByRole("status", { name: "Loading" }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("navigation", { name: "Main menu" }),
      ).toBeInTheDocument();
      expect(screen.queryByText("Content")).not.toBeInTheDocument();
    });

    it("PlatformShell_NoSystemAdmin_ReplacesTheShellWithNoAccess", async () => {
      server.use(
        getGetMeMockHandler(
          getGetMeResponseMock({ isSystemAdmin: false, language: "en" }),
        ),
        http.get("*/v1/platform/tenants", () =>
          HttpResponse.json(refused, { status: 403 }),
        ),
      );

      renderWithProviders(<PlatformShell>Content</PlatformShell>);

      expect(
        await screen.findByRole("heading", { name: "You don’t have access" }),
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("navigation", { name: "Main menu" }),
      ).not.toBeInTheDocument();
    });

    it("PlatformShell_GetMeSaysSuspended_ReplacesTheShellWithTheAccountPage", async () => {
      server.use(
        http.get("*/v1/me", () =>
          HttpResponse.json(
            {
              status: 403,
              title: "Forbidden",
              code: ErrorCode.UserSuspended,
              traceId: "00-trace-03",
            },
            { status: 403 },
          ),
        ),
      );

      renderWithProviders(<PlatformShell>Content</PlatformShell>);

      expect(
        await screen.findByRole("heading", {
          name: "Your account is suspended",
        }),
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("navigation", { name: "Main menu" }),
      ).not.toBeInTheDocument();
    });
  });
});
