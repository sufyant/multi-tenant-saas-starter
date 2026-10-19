import { type MyTenantResponse, Permission } from "@repo/api-client";
import {
  getGetMyTenantMockHandler,
  getGetMyTenantResponseMock,
} from "@repo/api-client/msw";
import { screen } from "@testing-library/react";
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
import { renderWithProviders } from "../../test/render";
import { TenantProvider, useCan, useTenant } from "./tenant-context";

vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({ signOut: vi.fn() }),
  useUser: () => ({
    user: { primaryEmailAddress: { emailAddress: "ali.kaya@gmail.com" } },
  }),
}));
const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const northwind: MyTenantResponse = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
  name: "Northwind Traders",
  slug: "northwind",
  status: "active",
  role: { builtIn: "member", name: "member" },
};

function Probe() {
  const { tenant } = useTenant();
  const can = useCan();
  return (
    <p>
      {tenant.name} {String(can(Permission.TenantMembersRead))}{" "}
      {String(can(Permission.TenantMembersDelete))}
    </p>
  );
}

const tenants = (items: MyTenantResponse[]) =>
  http.get("*/v1/me/tenants", () =>
    HttpResponse.json({ items, page: 1, pageSize: 100, total: items.length }),
  );

describe("TenantProvider", () => {
  it("TenantProvider_SlugInTheList_ProvidesTheTenantAndItsPermissions", async () => {
    server.use(
      tenants([northwind]),
      getGetMyTenantMockHandler(
        getGetMyTenantResponseMock({
          ...northwind,
          permissions: [Permission.TenantMembersRead],
        }),
      ),
    );

    renderWithProviders(
      <TenantProvider slug="northwind">
        <Probe />
      </TenantProvider>,
    );

    expect(
      await screen.findByText("Northwind Traders true false"),
    ).toBeInTheDocument();
  });

  it("TenantProvider_SlugNotInTheList_SaysNoAccessYet", async () => {
    server.use(tenants([]));

    renderWithProviders(
      <TenantProvider slug="atlas">
        <Probe />
      </TenantProvider>,
    );

    expect(
      await screen.findByRole("heading", { name: "No access yet" }),
    ).toBeInTheDocument();
  });
});
