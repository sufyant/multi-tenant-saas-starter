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

const shannon: MyTenantResponse = {
  ...northwind,
  id: "0198f0a2-0000-7000-8000-00000000a73a",
  name: "Shannon Studio",
  slug: "shannon",
  status: MyTenantResponseStatus.Suspended,
};

const kestrel: MyTenantResponse = {
  ...northwind,
  id: "0198f0a2-0000-7000-8000-00000000a74a",
  name: "Kestrel Labs",
  slug: "kestrel",
  status: MyTenantResponseStatus.Closed,
};

describe("ManageNotifications", () => {
  it.each([
    ["One", [northwind], "/northwind/settings/notifications"],
    ["Several", [atlas, northwind], "/atlas/settings/notifications"],
    [
      "SeveralWithAnInactiveFirst",
      [shannon, northwind],
      "/northwind/settings/notifications",
    ],
    ["OnlyInactive", [shannon, kestrel], "/"],
    ["None", [], "/"],
  ])(
    "ManageNotifications_%sOrganisations_OpensTheFirstActiveOnesPageElseTheStartPage",
    async (_, items, href) => {
      server.use(tenants(items));

      renderWithProviders(<ManageNotifications />);

      await vi.waitFor(() => expect(replace).toHaveBeenCalledWith(href));
    },
  );
});
