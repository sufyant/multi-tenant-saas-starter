import { ErrorCode } from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
} from "@repo/api-client/msw";
import { screen, within } from "@testing-library/react";
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
import { problem } from "../../../test/tenant";
import { PersonalShell } from "./personal-shell";

const route = vi.hoisted(() => ({ pathname: "/settings/profile" }));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
  usePathname: () => route.pathname,
}));
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({ signOut: vi.fn(), loaded: true }),
  useUser: () => ({
    user: { primaryEmailAddress: { emailAddress: "lena@northwind.io" } },
  }),
}));
vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  route.pathname = "/settings/profile";
});
afterAll(() => server.close());

/** A person without an organisation: GetMe, an empty ListMyTenants and the personal bell's count. */
const answers = [
  getGetMeMockHandler(
    getGetMeResponseMock({
      name: "Lena Fischer",
      email: "lena@northwind.io",
      language: "en",
    }),
  ),
  http.get("*/v1/me/tenants", () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 100, total: 0 }),
  ),
  http.get("*/v1/me/notifications/unread-count", () =>
    HttpResponse.json({ unread: 2 }),
  ),
];

describe("PersonalShell", () => {
  // 5.11 and 6.33: Settings without an organisation: Back to my organisations and My account, no Organisation group.
  it("PersonalShell_Shown_ShowsBackToMyOrganisationsAndMyAccount", async () => {
    server.use(...answers);

    renderWithProviders(<PersonalShell>Content</PersonalShell>);

    await screen.findByText("Content");
    const menu = screen.getByRole("navigation", { name: "Settings menu" });
    expect(
      within(menu)
        .getAllByRole("link")
        .map((link) => [link.textContent, link.getAttribute("href")]),
    ).toEqual([
      ["Back to my organisations", "/"],
      ["Profile", "/settings/profile"],
      ["Account and security", "/settings/account"],
    ]);
    expect(within(menu).getByRole("link", { name: "Profile" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("PersonalShell_Breadcrumb_IsSettingsThenThePage", async () => {
    route.pathname = "/settings/account";
    server.use(...answers);

    renderWithProviders(<PersonalShell>Content</PersonalShell>);

    await screen.findByText("Content");
    const crumbs = within(
      screen.getByRole("navigation", { name: "Breadcrumb" }),
    ).getAllByRole("listitem");
    expect(crumbs.map((crumb) => crumb.textContent)).toEqual([
      "Settings",
      "Account and security",
    ]);
  });

  // The account menu at the foot of the settings menu names the person (5.11).
  it("PersonalShell_AccountMenu_NamesThePerson", async () => {
    server.use(...answers);

    renderWithProviders(<PersonalShell>Content</PersonalShell>);

    expect(
      await screen.findByRole("button", { name: /Lena Fischer/ }),
    ).toBeInTheDocument();
  });

  it("PersonalShell_GetMeSaysSuspended_ShowsTheAccountsPage", async () => {
    server.use(
      http.get("*/v1/me", () => problem(403, ErrorCode.UserSuspended)),
      ...answers.slice(1),
    );

    renderWithProviders(<PersonalShell>Content</PersonalShell>);

    expect(
      await screen.findByRole("heading", { name: "Your account is suspended" }),
    ).toBeInTheDocument();
    expect(screen.queryByText("Content")).not.toBeInTheDocument();
  });
});
