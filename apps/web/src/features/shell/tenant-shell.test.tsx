import {
  ErrorCode,
  getGetMyTenantQueryKey,
  type MyTenantResponse,
  Permission,
} from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
  getGetMyTenantMockHandler,
  getGetMyTenantResponseMock,
} from "@repo/api-client/msw";
import { useQueryClient } from "@tanstack/react-query";
import { fireEvent, screen, within } from "@testing-library/react";
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
import { lastOrganisationCookie } from "@/shared/last-organisation-cookie";
import { TenantProvider } from "@/shared/tenant-context";
import { cookieStore } from "../../../test/cookies";
import { renderWithProviders } from "../../../test/render";
import { problem, roleOf } from "../../../test/tenant";
import {
  OrganisationShell,
  PendingTenantShell,
  TenantShell,
} from "./tenant-shell";

const route = vi.hoisted(() => ({ pathname: "/northwind" }));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
  usePathname: () => route.pathname,
}));
// Clerk is not ours: the test fakes its hooks. `loaded` tells whether Clerk's script has loaded (F13).
const clerk = vi.hoisted(() => ({ loaded: true }));
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({ signOut: vi.fn(), loaded: clerk.loaded }),
  useUser: () => ({
    user: { primaryEmailAddress: { emailAddress: "ayse@northwind.io" } },
  }),
}));
vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  route.pathname = "/northwind";
  clerk.loaded = true;
});
afterAll(() => server.close());

const northwind: MyTenantResponse = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
  name: "Northwind Traders",
  slug: "northwind",
  status: "active",
  role: roleOf.owner,
};

const harbour: MyTenantResponse = {
  ...northwind,
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e30",
  name: "Blue Harbour Logistics",
  slug: "blue-harbour",
  status: "suspended",
  role: roleOf.member,
};

const tenantsOf = (total: number) =>
  http.get("*/v1/me/tenants", () =>
    HttpResponse.json({
      items: [northwind, harbour].slice(0, total),
      page: 1,
      pageSize: 100,
      total,
    }),
  );

const answers = (total: number) => [
  tenantsOf(total),
  http.get(`*/v1/tenants/${northwind.id}/notifications/unread-count`, () =>
    HttpResponse.json({ unread: 4 }),
  ),
  getGetMyTenantMockHandler(
    getGetMyTenantResponseMock({
      ...northwind,
      permissions: [Permission.TenantMembersRead],
    }),
  ),
  getGetMeMockHandler(
    getGetMeResponseMock({
      name: "Ayşe Yılmaz",
      language: "en",
    }),
  ),
];

/** GetMyTenant read again, as after a focus or a signal: the page's view of the role follows. */
function ReadTheTenantAgain() {
  const queryClient = useQueryClient();
  return (
    <button
      type="button"
      onClick={() =>
        void queryClient.invalidateQueries({
          queryKey: getGetMyTenantQueryKey(northwind.id),
        })
      }
    >
      Read again
    </button>
  );
}

const shell = () => (
  <TenantProvider
    slug="northwind"
    pending={<PendingTenantShell slug="northwind" />}
  >
    <TenantShell>Content</TenantShell>
  </TenantProvider>
);

describe("TenantShell", () => {
  it("TenantShell_OrganisationOpened_RemembersItsSlugAsLastUsed", async () => {
    server.use(...answers(1));

    renderWithProviders(shell());

    await screen.findByText("Content");
    await vi.waitFor(() =>
      expect(cookieStore.values.get(lastOrganisationCookie)).toBe("northwind"),
    );
  });

  // A server action that sets a cookie renders the page again; before Clerk's script has loaded, that render can reach the proxy
  // without the dev browser cookie and leave for sign-in (F13). So the slug waits for Clerk.
  it("TenantShell_ClerkNotLoaded_RemembersNothingYet", async () => {
    clerk.loaded = false;
    server.use(...answers(1));

    renderWithProviders(shell());

    await screen.findByText("Content");
    expect(cookieStore.set).not.toHaveBeenCalled();
  });

  it("TenantShell_MainMenu_ShowsTheOrganisationsBellHomeAndSettings", async () => {
    server.use(...answers(1));

    renderWithProviders(shell());

    await screen.findByText("Content");
    const menu = screen.getByRole("navigation", { name: "Main menu" });
    expect(
      await within(menu).findByRole("button", {
        name: "Notifications, 4 unread",
      }),
    ).toBeInTheDocument();
    expect(
      within(menu)
        .getAllByRole("link")
        .map((link) => link.textContent),
    ).toEqual(["Home", "Settings"]);
  });

  it("TenantShell_Settings_OpensProfile", async () => {
    server.use(...answers(1));

    renderWithProviders(shell());

    const menu = await screen.findByRole("navigation", { name: "Main menu" });
    expect(
      within(menu).getByRole("link", { name: "Settings" }),
    ).toHaveAttribute("href", "/northwind/settings/profile");
  });

  it("TenantShell_Breadcrumb_IsTheOrganisationThenTheHome", async () => {
    server.use(...answers(1));

    renderWithProviders(shell());

    await screen.findByText("Content");
    const crumbs = within(
      screen.getByRole("navigation", { name: "Breadcrumb" }),
    ).getAllByRole("listitem");
    expect(crumbs.map((crumb) => crumb.textContent)).toEqual([
      "Northwind Traders",
      "Home",
    ]);
  });

  it("TenantShell_NotFound_NamesThePageAndMarksNoRow", async () => {
    route.pathname = "/northwind/an-old-link";
    server.use(...answers(1));

    renderWithProviders(
      <TenantProvider slug="northwind" pending={null}>
        <TenantShell notFound>Content</TenantShell>
      </TenantProvider>,
    );

    await screen.findByText("Content");
    const crumbs = within(
      screen.getByRole("navigation", { name: "Breadcrumb" }),
    ).getAllByRole("listitem");
    expect(crumbs.map((crumb) => crumb.textContent)).toEqual([
      "Northwind Traders",
      "Page not found",
    ]);
    expect(screen.getByRole("link", { name: "Home" })).not.toHaveAttribute(
      "aria-current",
    );
  });

  it("TenantShell_InSettings_ShowsBackMyAccountAndThePagesTheRoleCanOpen", async () => {
    route.pathname = "/northwind/settings/members";
    server.use(...answers(1));

    renderWithProviders(shell());

    await screen.findByText("Content");
    const menu = screen.getByRole("navigation", { name: "Settings menu" });
    // The role reads members only: General, Roles and Activity log are not in its menu.
    expect(
      within(menu)
        .getAllByRole("link")
        .map((link) => link.textContent),
    ).toEqual([
      "Back to Northwind Traders",
      "Profile",
      "Account and security",
      "Members",
    ]);
    expect(within(menu).getByRole("link", { name: "Members" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("TenantShell_InSettings_CountsTheMembersAndTheRoles", async () => {
    route.pathname = "/northwind/settings/members";
    server.use(
      // The first handler of a path answers: this role reads members and roles.
      getGetMyTenantMockHandler(
        getGetMyTenantResponseMock({
          ...northwind,
          permissions: [
            Permission.TenantMembersRead,
            Permission.TenantRolesRead,
          ],
        }),
      ),
      ...answers(1),
      http.get(`*/v1/tenants/${northwind.id}/members`, () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 1, total: 11 }),
      ),
      http.get(`*/v1/tenants/${northwind.id}/roles`, () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 100, total: 8 }),
      ),
    );

    renderWithProviders(shell());

    await screen.findByText("Content");
    const menu = screen.getByRole("navigation", { name: "Settings menu" });
    expect(
      await within(menu).findByRole("link", { name: /^Members ?11$/ }),
    ).toBeInTheDocument();
    expect(
      await within(menu).findByRole("link", { name: /^Roles ?8$/ }),
    ).toBeInTheDocument();
  });

  it("TenantShell_TabTitle_IsTheUnreadCountThePageAndTheProduct", async () => {
    server.use(...answers(1));

    renderWithProviders(shell());

    await vi.waitFor(() => expect(document.title).toBe("(4) Home · Starter"));
  });

  it("TenantShell_CommandMenu_GroupsGoToAndSettingsWithoutAnInviteTheRoleCannotSend", async () => {
    server.use(...answers(1));
    renderWithProviders(shell());
    await screen.findByText("Content");

    fireEvent.keyDown(window, { key: "k", metaKey: true });
    const dialog = await screen.findByRole("dialog", { name: "Command menu" });

    expect(within(dialog).getByText("Go to")).toBeInTheDocument();
    expect(
      within(dialog).getByText("Settings · My account"),
    ).toBeInTheDocument();
    expect(
      within(dialog).getByText("Settings · Organisation"),
    ).toBeInTheDocument();
    expect(
      within(dialog).queryByRole("option", { name: "Invite member" }),
    ).not.toBeInTheDocument();
  });

  it("TenantShell_CommandMenu_DrawsAnIconBeforeEachAction", async () => {
    server.use(
      getGetMyTenantMockHandler(
        getGetMyTenantResponseMock({
          ...northwind,
          permissions: [
            Permission.TenantMembersRead,
            Permission.TenantInvitationsCreate,
          ],
        }),
      ),
      ...answers(2),
    );
    renderWithProviders(shell());
    await screen.findByText("Content");

    fireEvent.keyDown(window, { key: "k", metaKey: true });
    const dialog = await screen.findByRole("dialog", { name: "Command menu" });

    // The icon, then the check that every item has.
    expect(
      within(dialog)
        .getByRole("option", { name: "Invite member" })
        .querySelectorAll("svg"),
    ).toHaveLength(2);
    expect(
      within(dialog)
        .getByRole("option", { name: "Switch organisation" })
        .querySelectorAll("svg"),
    ).toHaveLength(2);
  });

  it("TenantShell_RoleChangesWhileOpen_SaysSoOnThePage", async () => {
    let reads = 0;
    server.use(
      tenantsOf(1),
      http.get(`*/v1/tenants/${northwind.id}/notifications/unread-count`, () =>
        HttpResponse.json({ unread: 0 }),
      ),
      http.get(`*/v1/tenants/${northwind.id}`, () => {
        reads += 1;
        return HttpResponse.json(
          getGetMyTenantResponseMock({
            ...northwind,
            role: reads === 1 ? roleOf.owner : roleOf.member,
            permissions: [Permission.TenantMembersRead],
          }),
        );
      }),
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
    );
    renderWithProviders(
      <TenantProvider slug="northwind">
        <TenantShell>
          <ReadTheTenantAgain />
        </TenantShell>
      </TenantProvider>,
    );

    await userEvent.click(
      await screen.findByRole("button", { name: "Read again" }),
    );

    expect(
      await screen.findByText("Your role changed to Member"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("This page now shows what a Member can do."),
    ).toBeInTheDocument();
  });

  it("TenantShell_OneOrganisation_ShowsNoSwitcherMenu", async () => {
    server.use(...answers(1));

    renderWithProviders(shell());

    await screen.findByText("Content");
    expect(
      screen.queryByRole("button", { name: /Northwind Traders/ }),
    ).not.toBeInTheDocument();
  });

  it("TenantShell_ManyOrganisations_OpensTheSwitcherFromTheName", async () => {
    server.use(...answers(2));

    renderWithProviders(shell());

    await userEvent.click(
      await screen.findByRole("button", { name: /Northwind Traders/ }),
    );

    const item = await screen.findByRole("menuitem", {
      name: /Blue Harbour Logistics/,
    });
    expect(item).toHaveAttribute("href", "/blue-harbour");
    expect(within(item).getByText("Suspended")).toBeInTheDocument();
    expect(within(item).getByText("Member")).toBeInTheDocument();
  });

  it.each([
    [ErrorCode.UserSuspended, "Your account is suspended", "Sign out"],
    [ErrorCode.UserDeleted, "This account was deleted", "Use another email"],
  ])(
    "TenantShell_GetMeSays_%s_ShowsTheAccountPageInPlaceOfTheShell",
    async (code, title, action) => {
      server.use(
        ...answers(1).slice(0, 2),
        http.get("*/v1/me", () =>
          HttpResponse.json(
            { status: 403, title: "Refused", code, traceId: "00-trace-01" },
            { status: 403 },
          ),
        ),
      );

      renderWithProviders(shell());

      expect(
        await screen.findByRole("heading", { name: title }),
      ).toBeInTheDocument();
      expect(screen.getByRole("button", { name: action })).toBeInTheDocument();
      expect(screen.queryByText("Content")).not.toBeInTheDocument();
      expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
    },
  );

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
    const waiting = () => http.get("*/v1/me/tenants", held);
    const meWaits = () => http.get("*/v1/me", held);

    it("TenantShell_ListMyTenantsWaits_ShowsTheMenuAndTheBreadcrumbAtOnce", () => {
      server.use(waiting(), meWaits());

      renderWithProviders(shell());

      const menu = screen.getByRole("navigation", { name: "Main menu" });
      expect(
        within(menu).getByRole("link", { name: "Settings" }),
      ).toHaveAttribute("href", "/northwind/settings/profile");
      expect(
        screen.getByRole("navigation", { name: "Breadcrumb" }),
      ).toHaveTextContent("Home");
      expect(screen.queryByText("Content")).not.toBeInTheDocument();
    });

    it("TenantShell_ListMyTenantsWaits_ShowsNoSkeletonBeforeTheDelayAndTheSkeletonAfterIt", async () => {
      server.use(waiting(), meWaits());

      renderWithProviders(shell());

      expect(screen.queryByRole("status")).not.toBeInTheDocument();
      expect(
        await screen.findByRole("status", { name: "Loading" }),
      ).toBeInTheDocument();
      expect(screen.getByRole("main")).toContainElement(
        screen.getByRole("status", { name: "Loading" }),
      );
    });

    it("TenantShell_ListMyTenantsWaits_HoldsTheNameWithASkeletonAfterTheDelay", async () => {
      server.use(waiting(), meWaits());

      renderWithProviders(shell());

      const menu = screen.getByRole("navigation", { name: "Main menu" });
      const before = menu.querySelectorAll("[data-slot='skeleton']");
      await screen.findByRole("status", { name: "Loading" });

      expect(before).toHaveLength(0);
      expect(menu.querySelector("[data-slot='skeleton']")).toHaveAttribute(
        "aria-hidden",
        "true",
      );
    });

    it("TenantShell_GetMyTenantWaits_KeepsTheShellAndWaitsInTheContent", async () => {
      server.use(
        tenantsOf(1),
        http.get("*/v1/tenants/:tenantId", held),
        meWaits(),
      );

      renderWithProviders(shell());

      expect(
        await screen.findByRole("status", { name: "Loading" }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("navigation", { name: "Main menu" }),
      ).toBeInTheDocument();
      expect(screen.queryByText("Content")).not.toBeInTheDocument();
    });

    it("TenantShell_SlugNotInTheList_ReplacesTheShellWithNoAccessYet", async () => {
      server.use(
        http.get("*/v1/me/tenants", () =>
          HttpResponse.json({ items: [], page: 1, pageSize: 100, total: 0 }),
        ),
        meWaits(),
      );

      renderWithProviders(shell());

      expect(
        await screen.findByRole("heading", { name: "No access yet" }),
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("navigation", { name: "Main menu" }),
      ).not.toBeInTheDocument();
    });

    it("TenantShell_SuspendedInTheList_ReplacesTheShellWithTheSuspendedPage", async () => {
      server.use(
        http.get("*/v1/me/tenants", () =>
          HttpResponse.json({
            items: [{ ...northwind, status: "suspended" }],
            page: 1,
            pageSize: 100,
            total: 1,
          }),
        ),
        meWaits(),
      );

      renderWithProviders(shell());

      expect(
        await screen.findByRole("heading", {
          name: "Northwind Traders is suspended",
        }),
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("navigation", { name: "Main menu" }),
      ).not.toBeInTheDocument();
    });

    it("TenantShell_GetMyTenantSaysClosed_ReplacesTheShellWithTheClosedPage", async () => {
      server.use(
        tenantsOf(1),
        http.get("*/v1/tenants/:tenantId", () =>
          problem(403, ErrorCode.TenantClosed),
        ),
        meWaits(),
      );

      renderWithProviders(shell());

      expect(
        await screen.findByRole("heading", {
          name: "Northwind Traders is closed",
        }),
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("navigation", { name: "Main menu" }),
      ).not.toBeInTheDocument();
    });

    it("OrganisationShell_GetMyTenantFails_ShowsTheProblemInTheShellsContent", async () => {
      server.use(
        tenantsOf(1),
        http.get("*/v1/tenants/:tenantId", () =>
          problem(500, ErrorCode.ServerError),
        ),
        getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      );

      renderWithProviders(
        <OrganisationShell slug="northwind">Content</OrganisationShell>,
      );

      expect(
        await screen.findByRole("heading", { name: "Something went wrong" }),
      ).toBeInTheDocument();
      expect(screen.getByRole("main")).toContainElement(
        screen.getByRole("alert"),
      );
      expect(
        screen.getByRole("navigation", { name: "Main menu" }),
      ).toBeInTheDocument();
      expect(screen.queryByText("Content")).not.toBeInTheDocument();
    });

    it("OrganisationShell_GetMyTenantSaysSuspendedUser_ReplacesTheShellWithTheAccountPage", async () => {
      server.use(
        tenantsOf(1),
        http.get("*/v1/tenants/:tenantId", () =>
          problem(403, ErrorCode.UserSuspended),
        ),
        meWaits(),
      );

      renderWithProviders(
        <OrganisationShell slug="northwind">Content</OrganisationShell>,
      );

      expect(
        await screen.findByRole("heading", {
          name: "Your account is suspended",
        }),
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("navigation", { name: "Main menu" }),
      ).not.toBeInTheDocument();
    });

    it("OrganisationShell_GetMyTenantNotFound_ReplacesTheShellWithNoAccessYet", async () => {
      server.use(
        tenantsOf(1),
        http.get("*/v1/tenants/:tenantId", () =>
          problem(404, ErrorCode.TenantNotFound),
        ),
        meWaits(),
      );

      renderWithProviders(
        <OrganisationShell slug="northwind">Content</OrganisationShell>,
      );

      expect(
        await screen.findByRole("heading", { name: "No access yet" }),
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("navigation", { name: "Main menu" }),
      ).not.toBeInTheDocument();
    });

    it("TenantShell_ListMyTenantsSaysSuspendedUser_ReplacesTheShellWithTheAccountPage", async () => {
      server.use(
        http.get("*/v1/me/tenants", () =>
          problem(403, ErrorCode.UserSuspended),
        ),
        meWaits(),
      );

      renderWithProviders(shell());

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
