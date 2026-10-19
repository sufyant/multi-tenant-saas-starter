import { type MyTenantResponse, Permission } from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
  getGetMyTenantMockHandler,
  getGetMyTenantResponseMock,
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
import { TenantProvider } from "@/shared/tenant-context";
import { renderWithProviders } from "../../../test/render";
import { TenantShell } from "./tenant-shell";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
  usePathname: () => "/northwind/settings/preferences",
}));
vi.mock("@clerk/nextjs", () => ({ useClerk: () => ({ signOut: vi.fn() }) }));
vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const northwind: MyTenantResponse = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
  name: "Northwind MRO",
  slug: "northwind",
  status: "active",
  role: { builtIn: "owner", name: "owner" },
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
  <TenantProvider slug="northwind">
    <TenantShell>Content</TenantShell>
  </TenantProvider>
);

describe("TenantShell", () => {
  it("TenantShell_Breadcrumb_StartsWithTheOrganisation", async () => {
    server.use(...answers(1));

    renderWithProviders(shell());

    const crumbs = within(
      await screen.findByRole("navigation", { name: "Breadcrumb" }),
    ).getAllByRole("listitem");
    expect(crumbs[0]).toHaveTextContent("Northwind MRO");
    expect(screen.getAllByText("Preferences").length).toBeGreaterThan(0);
  });

  it("TenantShell_OneOrganisation_ShowsNoOrgMenu", async () => {
    server.use(...answers(1));

    renderWithProviders(shell());

    await screen.findByRole("navigation", { name: "Breadcrumb" });
    expect(
      screen.queryByRole("button", { name: "Northwind MRO" }),
    ).not.toBeInTheDocument();
  });

  it("TenantShell_ManyOrganisations_OpensTheOrgMenuFromTheName", async () => {
    server.use(...answers(2));

    renderWithProviders(shell());

    expect(
      await screen.findByRole("button", { name: "Northwind MRO" }),
    ).toBeInTheDocument();
  });
});
