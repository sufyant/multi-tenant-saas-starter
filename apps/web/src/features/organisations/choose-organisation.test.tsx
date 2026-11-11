import { ErrorCode, type MyTenantResponse } from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
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
import { lastOrganisationCookie } from "@/shared/last-organisation-cookie";
import { cookieStore } from "../../../test/cookies";
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

const personalCount = http.get("*/v1/me/notifications/unread-count", () =>
  HttpResponse.json({ unread: 1 }),
);

describe("ChooseOrganisation", () => {
  it("ChooseOrganisation_ManyOrganisations_ShowsThePersonalBell", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([
        tenant("Northwind Traders", "northwind"),
        tenant("Atlas Logistics", "atlas"),
      ]),
      personalCount,
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(
      await screen.findByRole("button", { name: "Notifications, 1 unread" }),
    ).toBeInTheDocument();
  });

  it("ChooseOrganisation_NoOrganisation_ShowsThePersonalBellOnNoAccessYet", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([]),
      personalCount,
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(
      await screen.findByRole("heading", { name: "No access yet" }),
    ).toBeInTheDocument();
    expect(
      await screen.findByRole("button", { name: "Notifications, 1 unread" }),
    ).toBeInTheDocument();
  });

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

  // Each organisation's tile is the info tint, as in the switcher (1.18).
  it("ChooseOrganisation_ManyOrganisations_DrawsEachTileInTheInfoTint", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      personalCount,
      page([
        tenant("Northwind Traders", "northwind"),
        tenant("Atlas Logistics", "atlas"),
      ]),
    );

    renderWithProviders(<ChooseOrganisation />);

    const link = await screen.findByRole("link", { name: /Northwind Traders/ });
    expect(within(link).getByText("N")).toHaveClass("bg-info-subtle", "size-8");
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
      await screen.findByText(
        "You are a member of 2 organisations. You can switch later from the sidebar.",
      ),
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

  // Frame 1.18: the name and the role, and "last used" after the role of the organisation the cookie names, which comes first.
  it("ChooseOrganisation_LastUsedSlug_SaysLastUsedAndListsItFirst", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([
        tenant("Atlas Logistics", "atlas"),
        tenant("Northwind Traders", "northwind"),
      ]),
    );

    renderWithProviders(<ChooseOrganisation lastUsed="northwind" />);

    const links = await screen.findAllByRole("link");
    expect(links.map((link) => link.getAttribute("href"))).toEqual([
      "/northwind",
      "/atlas",
    ]);
    expect(links[0]).toHaveTextContent("Owner · last used");
    expect(links[1]).toHaveTextContent(/^AAtlas LogisticsOwner$/);
    expect(cookieStore.delete).not.toHaveBeenCalled();
  });

  // The roadmap: a slug of an organisation the person can no longer open is ignored, and the cookie is deleted.
  it("ChooseOrganisation_LastUsedSlugNotInTheList_IgnoresAndForgetsIt", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([
        tenant("Atlas Logistics", "atlas"),
        tenant("Northwind Traders", "northwind"),
      ]),
    );

    renderWithProviders(<ChooseOrganisation lastUsed="kestrel" />);

    await screen.findAllByRole("link");
    expect(screen.queryByText(/last used/)).not.toBeInTheDocument();
    await vi.waitFor(() =>
      expect(cookieStore.delete).toHaveBeenCalledExactlyOnceWith(
        lastOrganisationCookie,
      ),
    );
  });

  it("ChooseOrganisation_LastUsedSlugOfASuspendedOrganisation_IgnoresAndForgetsIt", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([
        tenant("Northwind Traders", "northwind"),
        { ...tenant("Atlas Logistics", "atlas"), status: "suspended" },
      ]),
    );

    renderWithProviders(<ChooseOrganisation lastUsed="atlas" />);

    await screen.findByText("Suspended");
    expect(screen.queryByText(/last used/)).not.toBeInTheDocument();
    await vi.waitFor(() =>
      expect(cookieStore.delete).toHaveBeenCalledExactlyOnceWith(
        lastOrganisationCookie,
      ),
    );
  });

  it("ChooseOrganisation_NoCookie_DeletesNothing", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([
        tenant("Atlas Logistics", "atlas"),
        tenant("Northwind Traders", "northwind"),
      ]),
    );

    renderWithProviders(<ChooseOrganisation />);

    await screen.findAllByRole("link");
    expect(cookieStore.delete).not.toHaveBeenCalled();
  });

  it("ChooseOrganisation_ManyOrganisations_OffersSignOut", async () => {
    server.use(
      getGetMeMockHandler(getGetMeResponseMock({ language: "en" })),
      page([
        tenant("Atlas Logistics", "atlas"),
        tenant("Northwind Traders", "northwind"),
      ]),
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(
      await screen.findByRole("button", { name: "Sign out" }),
    ).toBeInTheDocument();
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

  // Frame 1.19: the account button at the top end, with the person's name; its menu holds Profile, which opens without an
  // organisation, and Sign out.
  it("ChooseOrganisation_NoOrganisation_ShowsTheAccountMenuWithProfile", async () => {
    server.use(
      getGetMeMockHandler(
        getGetMeResponseMock({
          language: "en",
          name: "Lena Fischer",
          email: "lena@northwind.example",
        }),
      ),
      page([]),
    );
    const user = userEvent.setup();

    renderWithProviders(<ChooseOrganisation />);

    await user.click(
      await screen.findByRole("button", { name: /Lena Fischer/ }),
    );
    const menu = await screen.findByRole("menu");
    expect(
      within(menu).getByText("lena@northwind.example"),
    ).toBeInTheDocument();
    expect(
      within(menu).getByRole("menuitem", { name: "Sign out" }),
    ).toBeInTheDocument();
    expect(
      within(menu).getByRole("menuitem", { name: "Profile" }),
    ).toHaveAttribute("href", "/settings/profile");
  });

  it("ChooseOrganisation_NotAUserYet_NamesTheAccountByItsAddress", async () => {
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
      await screen.findByRole("button", { name: /ali\.kaya@gmail\.com/ }),
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
    },
  );

  // Frames 1.40 and 1.44: a suspended account offers Contact support, with the version, and Sign out; a deleted one only
  // another email.
  it("ChooseOrganisation_GetMeSaysSuspended_OffersContactSupportAndSignOut", async () => {
    server.use(
      http.get("*/v1/me", () =>
        HttpResponse.json(
          {
            status: 403,
            title: "Refused",
            code: ErrorCode.UserSuspended,
            traceId: "00-trace-01",
          },
          { status: 403 },
        ),
      ),
      page([tenant("Northwind Traders", "northwind")]),
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(
      await screen.findByRole("button", { name: "Contact support" }),
    ).toHaveAttribute("href", expect.stringMatching(/^mailto:/));
    expect(
      screen.getByRole("button", { name: "Sign out" }),
    ).toBeInTheDocument();
  });

  it("ChooseOrganisation_GetMeSaysDeleted_OffersOnlyAnotherEmail", async () => {
    server.use(
      http.get("*/v1/me", () =>
        HttpResponse.json(
          {
            status: 403,
            title: "Refused",
            code: ErrorCode.UserDeleted,
            traceId: "00-trace-01",
          },
          { status: 403 },
        ),
      ),
      page([tenant("Northwind Traders", "northwind")]),
    );

    renderWithProviders(<ChooseOrganisation />);

    expect(
      await screen.findByRole("button", { name: "Use another email" }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole("button")).toHaveLength(1);
  });
});
