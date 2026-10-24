import { ErrorCode, type MyTenantResponse } from "@repo/api-client";
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
import { roleOf } from "../../../test/tenant";
import { ChooseOrganisation } from "./choose-organisation";

const replace = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace, refresh: vi.fn(), push: vi.fn() }),
}));
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({ signOut: vi.fn() }),
  useUser: () => ({
    user: { primaryEmailAddress: { emailAddress: "ali.kaya@gmail.com" } },
  }),
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
  role: roleOf.owner,
});

const page = (items: MyTenantResponse[]) =>
  http.get("*/v1/me/tenants", () =>
    HttpResponse.json({ items, page: 1, pageSize: 100, total: items.length }),
  );

/** A line of text that spans several elements, such as the slug and the role. */
const line = (text: string) => (_: string, element: Element | null) =>
  element?.tagName === "SPAN" && element.textContent === text;

describe("ChooseOrganisation", () => {
  it("ChooseOrganisation_ManyOrganisations_ListsEachOne", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([
        tenant("Northwind Traders", "northwind"),
        tenant("Atlas Logistics", "atlas"),
      ]),
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(
      await screen.findByRole("link", { name: /Northwind Traders/ }),
    ).toHaveAttribute("href", "/northwind");
    expect(
      screen.getByRole("link", { name: /Atlas Logistics/ }),
    ).toHaveAttribute("href", "/atlas");
  });

  it("ChooseOrganisation_OneOrganisation_OpensIt", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([tenant("Northwind Traders", "northwind")]),
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

  it("ChooseOrganisation_SuspendedOrganisation_ShowsItsStatusAndNoLink", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([
        tenant("Northwind Traders", "northwind"),
        { ...tenant("Atlas Logistics", "atlas"), status: "suspended" },
      ]),
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(
      await screen.findByText("You belong to 2 organisations."),
    ).toBeInTheDocument();
    expect(screen.getByText("Suspended")).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: /Atlas Logistics/ }),
    ).not.toBeInTheDocument();
  });

  it("ChooseOrganisation_PurgingOrganisation_ShowsBeingDeletedAndNoLink", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([
        tenant("Northwind Traders", "northwind"),
        { ...tenant("Atlas Logistics", "atlas"), status: "purging" },
      ]),
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(await screen.findByText("Being deleted")).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: /Atlas Logistics/ }),
    ).not.toBeInTheDocument();
  });

  it("ChooseOrganisation_Row_ShowsTheSlugAndTheRoleOnOneLine", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([
        tenant("Northwind Traders", "northwind"),
        { ...tenant("Liffey Design", "liffey-design"), status: "purging" },
      ]),
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(
      await screen.findByText(line("northwind · Owner")),
    ).toBeInTheDocument();
    expect(screen.getByText(line("liffey-design · Owner"))).toBeInTheDocument();
  });

  it("ChooseOrganisation_PurgingInArabic_SaysBeingDeleted", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "ar" })),
      page([
        tenant("Northwind Traders", "northwind"),
        { ...tenant("Liffey Design", "liffey-design"), status: "purging" },
      ]),
    );

    renderWithProviders(<ChooseOrganisation />, "ar");

    expect(await screen.findByText("قيد الحذف")).toBeInTheDocument();
  });

  it("ChooseOrganisation_NoOrganisation_ShowsWhoIsSignedInAndSignOut", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([]),
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(await screen.findByText("ali.kaya@gmail.com")).toHaveAttribute(
      "dir",
      "ltr",
    );
    expect(
      screen.getByRole("button", { name: "Sign out" }),
    ).toBeInTheDocument();
  });

  it.each([
    [ErrorCode.UserSuspended, "Your account is suspended"],
    [ErrorCode.UserDeleted, "This account was deleted"],
  ])(
    "ChooseOrganisation_GetMeSays_%s_ShowsTheAccountPage",
    async (code, title) => {
      server.use(
        http.get("*/v1/me", () =>
          HttpResponse.json(
            { status: 403, title: "Refused", code, traceId: "00-trace-01" },
            { status: 403 },
          ),
        ),
        page([tenant("Northwind Traders", "northwind")]),
      );

      renderWithProviders(<ChooseOrganisation />);

      expect(
        await screen.findByRole("heading", { name: title }),
      ).toBeInTheDocument();
      expect(screen.getByText("ali.kaya@gmail.com")).toBeInTheDocument();
    },
  );
});
