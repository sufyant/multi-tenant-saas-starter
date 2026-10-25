import { ErrorCode, type MyTenantResponse, Permission } from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
  getGetMyTenantMockHandler,
  getGetMyTenantResponseMock,
} from "@repo/api-client/msw";
import { screen, within } from "@testing-library/react";
import { delay, HttpResponse, http } from "msw";
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
import { TenantProvider } from "@/shared/tenant-context";
import { renderWithProviders } from "../../../test/render";
import { problem, roleOf } from "../../../test/tenant";
import { PendingTenantShell, TenantShell } from "./tenant-shell";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
  usePathname: () => "/northwind/settings/preferences",
}));
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({ signOut: vi.fn() }),
  useUser: () => ({
    user: { primaryEmailAddress: { emailAddress: "ayse@northwind.io" } },
  }),
}));
vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const northwind: MyTenantResponse = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
  name: "Northwind Traders",
  slug: "northwind",
  status: "active",
  role: roleOf.owner,
};

const answers = (total: number) => [
  http.get("*/v1/me/tenants", () =>
    HttpResponse.json({ items: [northwind], page: 1, pageSize: 100, total }),
  ),
  getGetMyTenantMockHandler(
    getGetMyTenantResponseMock({
      ...northwind,
      permissions: [Permission.TenantMembersRead],
    }),
  ),
  getGetMeMockHandler(
    getGetMeResponseMock({
      firstName: "Ayşe",
      lastName: "Yılmaz",
      language: "en",
    }),
  ),
];

const shell = () => (
  <TenantProvider
    slug="northwind"
    pending={<PendingTenantShell slug="northwind" railExpanded={false} />}
  >
    <TenantShell railExpanded={false}>Content</TenantShell>
  </TenantProvider>
);

describe("TenantShell", () => {
  it("TenantShell_RailSettings_OpensGeneral", async () => {
    server.use(...answers(1));

    renderWithProviders(shell());

    const rail = await screen.findByRole("navigation", { name: "Main" });
    expect(
      within(rail).getByRole("link", { name: "Settings" }),
    ).toHaveAttribute("href", "/northwind/settings/general");
  });

  it("TenantShell_Breadcrumb_StartsWithTheOrganisation", async () => {
    server.use(...answers(1));

    renderWithProviders(shell());

    await screen.findByText("Northwind Traders");
    const crumbs = within(
      screen.getByRole("navigation", { name: "Breadcrumb" }),
    ).getAllByRole("listitem");
    expect(crumbs[0]).toHaveTextContent("Northwind Traders");
    expect(screen.getAllByText("Preferences").length).toBeGreaterThan(0);
  });

  it("TenantShell_OneOrganisation_ShowsNoOrgMenu", async () => {
    server.use(...answers(1));

    renderWithProviders(shell());

    await screen.findByText("Northwind Traders");
    expect(
      screen.queryByRole("button", { name: "Northwind Traders" }),
    ).not.toBeInTheDocument();
  });

  it("TenantShell_ManyOrganisations_OpensTheOrgMenuFromTheName", async () => {
    server.use(...answers(2));

    renderWithProviders(shell());

    expect(
      await screen.findByRole("button", { name: "Northwind Traders" }),
    ).toBeInTheDocument();
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
    const waiting = () =>
      http.get("*/v1/me/tenants", async () => {
        await delay("infinite");
      });
    const meWaits = () =>
      http.get("*/v1/me", async () => {
        await delay("infinite");
      });

    it("TenantShell_ListMyTenantsWaits_ShowsTheRailAndTheTopBarAtOnce", () => {
      server.use(waiting(), meWaits());

      renderWithProviders(shell());

      const rail = screen.getByRole("navigation", { name: "Main" });
      expect(
        within(rail).getByRole("link", { name: "Settings" }),
      ).toHaveAttribute("href", "/northwind/settings/general");
      expect(screen.getByRole("banner")).toBeInTheDocument();
      expect(
        screen.getByRole("navigation", { name: "Breadcrumb" }),
      ).toHaveTextContent("Preferences");
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

    it("TenantShell_GetMyTenantWaits_KeepsTheShellAndWaitsInTheContent", async () => {
      server.use(
        answers(1)[0],
        http.get("*/v1/tenants/:tenantId", async () => {
          await delay("infinite");
        }),
        meWaits(),
      );

      renderWithProviders(shell());

      expect(
        await screen.findByRole("status", { name: "Loading" }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("navigation", { name: "Main" }),
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
        screen.queryByRole("navigation", { name: "Main" }),
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
        screen.queryByRole("navigation", { name: "Main" }),
      ).not.toBeInTheDocument();
    });

    it("TenantShell_GetMyTenantSaysClosed_ReplacesTheShellWithTheClosedPage", async () => {
      server.use(
        answers(1)[0],
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
        screen.queryByRole("navigation", { name: "Main" }),
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
        screen.queryByRole("navigation", { name: "Main" }),
      ).not.toBeInTheDocument();
    });
  });
});
