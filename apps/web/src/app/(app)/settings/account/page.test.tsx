import {
  DataExportResponseStatus,
  ErrorCode,
  Language,
} from "@repo/api-client";
import { getGetMeResponseMock } from "@repo/api-client/msw";
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
import { renderWithProviders } from "../../../../../test/render";
import PersonalSettingsLayout from "../layout";
import PersonalAccountPage from "./page";

// A person without an organisation opens Account and security at /settings/account, in the shell without an organisation, and
// can export their data and delete their account there (the roadmap's 37a-frontend-4).

const route = vi.hoisted(() => ({ pathname: "/settings/account" }));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
  usePathname: () => route.pathname,
}));
const clerk = vi.hoisted(() => ({
  user: {
    externalAccounts: [],
    totpEnabled: false,
    getSessions: async () => [],
    reload: async () => {},
  },
  session: { id: "sess_this" },
  signOut: vi.fn(async () => {}),
}));
vi.mock("@clerk/nextjs", () => ({
  useUser: () => ({ user: clerk.user }),
  useSession: () => ({ session: clerk.session }),
  useReverification: (change: unknown) => change,
  useClerk: () => ({ signOut: clerk.signOut, loaded: true }),
}));
vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  vi.clearAllMocks();
});
afterAll(() => server.close());

const traceId = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

/** Lena, who is in no organisation: GetMe, an empty ListMyTenants, the personal bell's count and no export yet. */
const withoutAnOrganisation = [
  http.get("*/v1/me", () =>
    HttpResponse.json(
      getGetMeResponseMock({
        name: "Lena Fischer",
        email: "lena@northwind.example",
        language: Language.En,
        timeZone: "Europe/Istanbul",
      }),
    ),
  ),
  http.get("*/v1/me/tenants", () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 100, total: 0 }),
  ),
  http.get("*/v1/me/notifications/unread-count", () =>
    HttpResponse.json({ unread: 0 }),
  ),
  http.get("*/v1/me/data-exports/latest", () =>
    HttpResponse.json(
      { code: ErrorCode.DataExportNotFound, traceId },
      { status: 404 },
    ),
  ),
];

const renderPage = () =>
  renderWithProviders(
    <PersonalSettingsLayout>
      <PersonalAccountPage />
    </PersonalSettingsLayout>,
  );

describe("PersonalAccountPage", () => {
  it("PersonalAccountPage_WithoutAnOrganisation_ExportsTheData", async () => {
    const keys: (string | null)[] = [];
    server.use(
      ...withoutAnOrganisation,
      http.post("*/v1/me/data-exports", ({ request }) => {
        keys.push(request.headers.get("Idempotency-Key"));
        return HttpResponse.json(
          {
            id: "0b6f7c6e-3d1a-4d4e-9a55-6a1f3c2b9e10",
            status: DataExportResponseStatus.Preparing,
            requestedAt: "2026-11-07T09:00:00Z",
            readyAt: null,
            expiresAt: null,
            sizeBytes: null,
          },
          { status: 201 },
        );
      }),
    );

    renderPage();
    const yourData = within(
      await screen.findByRole("region", { name: "Your data" }),
    );
    await userEvent.click(
      await yourData.findByRole("button", { name: "Export my data" }),
    );
    await userEvent.click(
      await screen.findByRole("button", { name: "Request export" }),
    );

    expect(await yourData.findByText("Preparing")).toBeInTheDocument();
    expect(keys).toEqual([expect.any(String)]);
  });

  it("PersonalAccountPage_WithoutAnOrganisation_DeletesTheAccount", async () => {
    const codes: unknown[] = [];
    server.use(
      ...withoutAnOrganisation,
      http.post("*/v1/me/account-deletion", () =>
        HttpResponse.json(
          { expiresAt: new Date(Date.now() + 600_000).toISOString() },
          { status: 201 },
        ),
      ),
      http.post("*/v1/me/account-deletion/confirm", async ({ request }) => {
        codes.push(await request.json());
        return new HttpResponse(null, { status: 204 });
      }),
    );

    renderPage();
    const card = within(
      await screen.findByRole("region", { name: "Delete my account" }),
    );
    await userEvent.click(
      await card.findByRole("button", { name: "Delete my account" }),
    );
    const step = within(
      await screen.findByRole("alertdialog", { name: "Delete your account?" }),
    );
    await userEvent.type(step.getByRole("textbox", { name: "Code" }), "381906");
    await userEvent.click(
      step.getByRole("button", { name: "Delete my account" }),
    );

    await expect.poll(() => clerk.signOut.mock.calls.length).toBe(1);
    expect(codes).toEqual([{ code: "381906" }]);
    expect(clerk.signOut).toHaveBeenCalledWith({
      redirectUrl: "/account/deleted",
    });
  });
});
