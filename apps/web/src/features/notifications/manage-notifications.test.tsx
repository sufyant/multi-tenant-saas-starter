import {
  type MyTenantResponse,
  MyTenantResponseStatus,
} from "@repo/api-client";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import { renderWithProviders } from "../../../test/render";
import { northwind } from "../../../test/tenant";
import { ManageNotifications } from "./manage-notifications";

const replace = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace, refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => vi.clearAllMocks());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const atlas: MyTenantResponse = {
  ...northwind,
  id: "0198f0a2-0000-7000-8000-00000000a71a",
  name: "Atlas Logistics",
  slug: "atlas",
  status: MyTenantResponseStatus.Active,
};

const tenants = (items: MyTenantResponse[]) =>
  http.get("*/v1/me/tenants", () =>
    HttpResponse.json({ items, page: 1, pageSize: 100, total: items.length }),
  );

describe("ManageNotifications", () => {
  it("ManageNotifications_OneOrganisation_OpensItsNotificationsPage", async () => {
    server.use(tenants([northwind]));

    renderWithProviders(<ManageNotifications />);

    await vi.waitFor(() =>
      expect(replace).toHaveBeenCalledWith("/northwind/settings/notifications"),
    );
  });

  it.each([
    ["Several", [northwind, atlas]],
    ["None", []],
  ])(
    "ManageNotifications_%sOrganisations_OpensTheStartPage",
    async (_, items) => {
      server.use(tenants(items));

      renderWithProviders(<ManageNotifications />);

      await vi.waitFor(() => expect(replace).toHaveBeenCalledWith("/"));
    },
  );
});
