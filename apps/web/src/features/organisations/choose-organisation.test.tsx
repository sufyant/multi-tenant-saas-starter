import type { MyTenantResponse } from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
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
import { renderWithProviders } from "../../../test/render";
import { ChooseOrganisation } from "./choose-organisation";

const replace = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace, refresh: vi.fn(), push: vi.fn() }),
}));
vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  replace.mockClear();
});
afterAll(() => server.close());

const tenant = (name: string, slug: string): MyTenantResponse => ({
  id: crypto.randomUUID(),
  name,
  slug,
  status: "active",
  role: { builtIn: "owner", name: "owner" },
});

const page = (items: MyTenantResponse[]) =>
  http.get("*/v1/me/tenants", () =>
    HttpResponse.json({ items, page: 1, pageSize: 100, total: items.length }),
  );

describe("ChooseOrganisation", () => {
  it("ChooseOrganisation_ManyOrganisations_ListsEachOne", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([
        tenant("Northwind MRO", "northwind"),
        tenant("Atlas Technic", "atlas"),
      ]),
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(
      await screen.findByRole("link", { name: /Northwind MRO/ }),
    ).toHaveAttribute("href", "/northwind");
    expect(screen.getByRole("link", { name: /Atlas Technic/ })).toHaveAttribute(
      "href",
      "/atlas",
    );
  });

  it("ChooseOrganisation_OneOrganisation_OpensIt", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([tenant("Northwind MRO", "northwind")]),
    );

    renderWithProviders(<ChooseOrganisation />);

    await vi.waitFor(() => expect(replace).toHaveBeenCalledWith("/northwind"));
  });

  it("ChooseOrganisation_NoOrganisation_SaysNoAccessYet", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([]),
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(
      await screen.findByRole("heading", { name: "No access yet" }),
    ).toBeInTheDocument();
  });

  it("ChooseOrganisation_NotAUserYet_SaysTheSameNoAccessYet", async () => {
    server.use(
      http.get("*/v1/me", () =>
        HttpResponse.json(
          { status: 404, code: "user.not_found", traceId: "t" },
          {
            status: 404,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
      page([]),
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(
      await screen.findByRole("heading", { name: "No access yet" }),
    ).toBeInTheDocument();
  });
});
