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
import { renderWithProviders } from "../../../test/render";
import { AccountPage } from "./account-page";

// Clerk is a system we do not own: the page's identity provider cards read a signed-in system admin with one session.
// Clerk's hooks give the same objects on every render, as Clerk does.
const clerk = vi.hoisted(() => ({
  user: {
    externalAccounts: [],
    totpEnabled: true,
    getSessions: async () => [],
    reload: async () => {},
  },
  session: { id: "sess_this" },
}));
vi.mock("@clerk/nextjs", () => ({
  useUser: () => ({ user: clerk.user }),
  useSession: () => ({ session: clerk.session }),
  useReverification: (change: unknown) => change,
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const me = getGetMeResponseMock({
  name: "Ciara Byrne",
  email: "ciara@starter.dev",
  isSystemAdmin: true,
  language: Language.En,
  timeZone: "Europe/Dublin",
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
  it("AccountPage_Shown_SaysTwoStepIsRequiredAndOn", async () => {
    server.use(http.get("*/v1/me", () => HttpResponse.json(me)));

    renderWithProviders(<AccountPage />);

    // The name is isolated from the sentence around it (F15).
    expect(await screen.findByText("Ciara Byrne")).toContainHTML(
      "<bdi>Ciara Byrne</bdi>",
    );
    expect(
      screen.getByText(
        /these settings are the same on the platform and in every organisation\.$/,
      ),
    ).toBeInTheDocument();
    expect(screen.getByText("ciara@starter.dev")).toBeInTheDocument();
    const twoStep = within(
      screen.getByRole("region", { name: "Two-step verification" }),
    );
    expect(
      twoStep.getByText(/^Required on the platform\./),
    ).toBeInTheDocument();
    expect(twoStep.getByText("On")).toBeInTheDocument();
    expect(twoStep.queryByRole("button")).toBeNull();
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
    );
    renderWithProviders(<AccountPage />);

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

  // A suspended account reads GetMe again, so the shell shows its page (F4, F47); the dialog says nothing of its own.
  it("AccountPage_ChangeEmailOfASuspendedAccount_ReadsGetMeAgain", async () => {
    let reads = 0;
    server.use(
      http.get("*/v1/me", () => {
        reads += 1;
        return HttpResponse.json(me);
      }),
      http.post("*/v1/me/email-change", () =>
        HttpResponse.json(
          {
            code: ErrorCode.UserSuspended,
            traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
          },
          { status: 403 },
        ),
      ),
    );
    renderWithProviders(<AccountPage />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Change email" }),
    );
    await userEvent.type(
      screen.getByRole("textbox", { name: "New email" }),
      "ayse@demir.example",
    );
    await userEvent.click(screen.getByRole("button", { name: "Send code" }));

    await expect.poll(() => reads).toBeGreaterThan(1);
    expect(screen.queryByRole("alertdialog")).toBeNull();
  });

  it("AccountPage_NoExportYet_OffersTheExportAndNoDeletion", async () => {
    server.use(
      http.get("*/v1/me", () => HttpResponse.json(me)),
      noExport,
    );

    renderWithProviders(<AccountPage />);

    const yourData = within(
      await screen.findByRole("region", { name: "Your data" }),
    );
    expect(
      await yourData.findByRole("button", { name: "Export my data" }),
    ).toBeInTheDocument();
    // The platform has no Delete my account (the maintainer's decision of 2026-11-05).
    expect(
      screen.queryByRole("region", { name: "Delete my account" }),
    ).not.toBeInTheDocument();
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
    );
    const assign = vi.fn();
    vi.stubGlobal("location", { ...window.location, assign });

    renderWithProviders(<AccountPage />);

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

  // The platform's shell does not watch the reads, so a read of Your data that refuses the account reads GetMe again, as the
  // email change does, and the shell shows its page (F47).
  it("AccountPage_ExportReadOfASuspendedAccount_ReadsGetMeAgain", async () => {
    let reads = 0;
    server.use(
      http.get("*/v1/me", () => {
        reads += 1;
        return HttpResponse.json(me);
      }),
      http.get("*/v1/me/data-exports/latest", () =>
        HttpResponse.json(
          {
            code: ErrorCode.UserSuspended,
            traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
          },
          { status: 403 },
        ),
      ),
    );

    renderWithProviders(<AccountPage />);

    await screen.findByRole("region", { name: "Your data" });
    await expect.poll(() => reads).toBeGreaterThan(1);
  });
});
