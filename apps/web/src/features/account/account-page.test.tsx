import {
  BuiltInRole,
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
import { renderInTenant, tenantAnswers } from "../../../test/tenant";
import { AccountPage } from "./account-page";

// Clerk is a system we do not own: the page's identity provider cards read a signed-in person with one session.
// Clerk's hooks give the same objects on every render, as Clerk does.
const clerk = vi.hoisted(() => ({
  user: {
    externalAccounts: [],
    totpEnabled: false,
    getSessions: async () => [],
    reload: async () => {},
  },
  session: { id: "sess_this" },
}));
vi.mock("@clerk/nextjs", () => ({
  useUser: () => ({ user: clerk.user }),
  useSession: () => ({ session: clerk.session }),
  useReverification: (change: unknown) => change,
  useClerk: () => ({ signOut: async () => {} }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const me = getGetMeResponseMock({
  name: "Ayşe Yılmaz",
  email: "ayse@northwind.io",
  language: Language.En,
  timeZone: "Europe/Istanbul",
});

/** No export yet: GetMyDataExport's 404. */
const noExport = http.get("*/v1/me/data-exports/latest", () =>
  HttpResponse.json(
    {
      code: ErrorCode.DataExportNotFound,
      traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    },
    { status: 404 },
  ),
);

const readyExport = {
  id: "0b6f7c6e-3d1a-4d4e-9a55-6a1f3c2b9e10",
  status: DataExportResponseStatus.Ready,
  requestedAt: "2026-11-05T09:00:00Z",
  readyAt: "2026-11-05T09:04:00Z",
  expiresAt: "2026-11-12T09:04:00Z",
  sizeBytes: 48_213,
};

describe("AccountPage", () => {
  it("AccountPage_Shown_NamesThePersonAndShowsTheirAddress", async () => {
    server.use(
      http.get("*/v1/me", () => HttpResponse.json(me)),
      noExport,
      ...tenantAnswers(BuiltInRole.Member),
    );

    renderInTenant(<AccountPage />, { url: "/northwind/settings/account" });

    // The name is isolated from the sentence around it (F15).
    expect(await screen.findByText("Ayşe Yılmaz")).toContainHTML(
      "<bdi>Ayşe Yılmaz</bdi>",
    );
    expect(
      screen.getByText(/these settings are the same in every organisation\.$/),
    ).toBeInTheDocument();
    expect(screen.getByText("ayse@northwind.io")).toBeInTheDocument();
    const twoStep = within(
      screen.getByRole("region", { name: "Two-step verification" }),
    );
    expect(twoStep.getByText(/^Optional\./)).toBeInTheDocument();
    expect(twoStep.getByText("Off")).toBeInTheDocument();
  });

  it("AccountPage_ChangeEmail_SendsTheKeyThenTheCodeAndReadsGetMeAgain", async () => {
    const sent: { key: string | null; body: unknown }[] = [];
    const codes: unknown[] = [];
    let reads = 0;
    server.use(
      http.get("*/v1/me", () => {
        reads += 1;
        return HttpResponse.json(me);
      }),
      noExport,
      http.post("*/v1/me/email-change", async ({ request }) => {
        sent.push({
          key: request.headers.get("Idempotency-Key"),
          body: await request.json(),
        });
        return HttpResponse.json(
          { expiresAt: new Date(Date.now() + 600_000).toISOString() },
          { status: 201 },
        );
      }),
      http.post("*/v1/me/email-change/confirm", async ({ request }) => {
        codes.push(await request.json());
        return HttpResponse.json({ ...me, email: "ayse@demir.example" });
      }),
      ...tenantAnswers(BuiltInRole.Member).filter(
        (handler) => !String(handler.info.path).endsWith("/v1/me"),
      ),
    );
    renderInTenant(<AccountPage />, { url: "/northwind/settings/account" });

    await userEvent.click(
      await screen.findByRole("button", { name: "Change email" }),
    );
    await userEvent.type(
      screen.getByRole("textbox", { name: "New email" }),
      "ayse@demir.example",
    );
    await userEvent.click(screen.getByRole("button", { name: "Send code" }));
    await userEvent.type(
      await screen.findByRole("textbox", { name: "Code" }),
      "381906",
    );
    await userEvent.click(screen.getByRole("button", { name: "Verify" }));

    expect(await screen.findByText("Email changed")).toBeInTheDocument();
    expect(sent).toEqual([
      { key: expect.any(String), body: { email: "ayse@demir.example" } },
    ]);
    expect(codes).toEqual([{ code: "381906" }]);
    expect(reads).toBeGreaterThan(1);
  });

  it("AccountPage_NoExportYet_OffersTheExportAndDeletion", async () => {
    server.use(
      http.get("*/v1/me", () => HttpResponse.json(me)),
      noExport,
      ...tenantAnswers(BuiltInRole.Member),
    );

    renderInTenant(<AccountPage />, { url: "/northwind/settings/account" });

    const yourData = within(
      await screen.findByRole("region", { name: "Your data" }),
    );
    expect(
      await yourData.findByRole("button", { name: "Export my data" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("region", { name: "Delete my account" }),
    ).toBeInTheDocument();
  });

  it("AccountPage_AReadyExport_DownloadsThroughAFreshLink", async () => {
    const downloads: string[] = [];
    server.use(
      http.get("*/v1/me", () => HttpResponse.json(me)),
      http.get("*/v1/me/data-exports/latest", () =>
        HttpResponse.json(readyExport),
      ),
      http.post("*/v1/me/data-exports/:exportId/download", ({ params }) => {
        downloads.push(String(params.exportId));
        return HttpResponse.json({
          url: "https://files.example/export.zip",
          expiresAt: "2026-11-06T10:05:00Z",
        });
      }),
      ...tenantAnswers(BuiltInRole.Member),
    );
    const assign = vi.fn();
    vi.stubGlobal("location", { ...window.location, assign });

    renderInTenant(<AccountPage />, { url: "/northwind/settings/account" });

    const yourData = within(
      await screen.findByRole("region", { name: "Your data" }),
    );
    expect(await yourData.findByText("Ready")).toBeInTheDocument();
    await userEvent.click(yourData.getByRole("button", { name: "Download" }));

    await expect
      .poll(() => assign.mock.calls)
      .toEqual([["https://files.example/export.zip"]]);
    expect(downloads).toEqual([readyExport.id]);
    vi.unstubAllGlobals();
  });
});
