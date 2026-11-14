import {
  ErrorCode,
  type MyTermsResponse,
  MyTermsResponseAction,
  type MyTermsVersionResponse,
} from "@repo/api-client";
import {
  getAcceptTermsVersionMockHandler,
  getGetMyTermsMockHandler,
} from "@repo/api-client/msw";
import { screen } from "@testing-library/react";
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
import { TermsGate } from "./terms-gate";

// Clerk is not ours, so the test fakes its hook (Khorikov, Chapter 9). The API is faked at the network (F35).
const signOut = vi.hoisted(() => vi.fn());
vi.mock("@clerk/nextjs", () => ({ useClerk: () => ({ signOut }) }));

const summary =
  "We now keep exported data files for 7 days, and section 6 names the companies that process your data.";
const december: MyTermsVersionResponse = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
  effectiveDate: "2026-12-01",
  url: "https://starter.example/terms/2026-12",
  summary,
  requiresAcceptance: true,
};
const asks = (version: MyTermsVersionResponse = december): MyTermsResponse => ({
  action: MyTermsResponseAction.Acceptance,
  version,
});
const nothing: MyTermsResponse = {
  action: MyTermsResponseAction.Nothing,
  version: null,
};

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  vi.clearAllMocks();
});
afterAll(() => server.close());

const renderGate = (locale: "en" | "ar" = "en") =>
  renderWithProviders(
    <TermsGate>
      <h1>Home</h1>
    </TermsGate>,
    locale,
  );

describe("TermsGate", () => {
  it("TermsGate_Nothing_ShowsThePage", async () => {
    server.use(getGetMyTermsMockHandler(nothing));

    renderGate();

    expect(
      await screen.findByRole("heading", { name: "Home" }),
    ).toBeInTheDocument();
  });

  it("TermsGate_GetMyTermsFails_ShowsThePage", async () => {
    server.use(http.get("*/v1/me/terms", () => HttpResponse.error()));

    renderGate();

    expect(
      await screen.findByRole("heading", { name: "Home" }),
    ).toBeInTheDocument();
  });

  it("TermsGate_Acceptance_ShowsTheGateInPlaceOfThePage", async () => {
    server.use(getGetMyTermsMockHandler(asks()));

    renderGate();

    // Frame 1.09: the title, the day it applies from, the link, the summary, and Accept disabled until the box is ticked.
    expect(
      await screen.findByRole("heading", { name: "Terms of use updated" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "The new Terms of use apply from 1 Dec 2026. Read them, then accept to keep using Starter.",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Read the terms ↗" }),
    ).toHaveAttribute("href", december.url);
    expect(screen.getByRole("region", { name: "Summary" })).toHaveTextContent(
      summary,
    );
    expect(screen.getByRole("button", { name: "Accept" })).toBeDisabled();
    expect(
      screen.queryByRole("heading", { name: "Home" }),
    ).not.toBeInTheDocument();
  });

  it("TermsGate_NoSummary_ShowsNoSummary", async () => {
    server.use(getGetMyTermsMockHandler(asks({ ...december, summary: null })));

    renderGate();

    await screen.findByRole("heading", { name: "Terms of use updated" });
    expect(
      screen.queryByRole("region", { name: "Summary" }),
    ).not.toBeInTheDocument();
  });

  it("TermsGate_Accept_SendsTheLinkReadAndShowsThePage", async () => {
    const accepted = vi.fn();
    server.use(
      getGetMyTermsMockHandler(asks()),
      getAcceptTermsVersionMockHandler(async ({ request, params }) => {
        accepted(params.versionId, await request.json());
        return nothing;
      }),
    );
    const user = userEvent.setup();
    renderGate();

    await user.click(
      await screen.findByRole("checkbox", {
        name: "I have read and agree to the Terms of use",
      }),
    );
    await user.click(screen.getByRole("button", { name: "Accept" }));

    expect(
      await screen.findByRole("heading", { name: "Home" }),
    ).toBeInTheDocument();
    expect(accepted).toHaveBeenCalledWith(december.id, { url: december.url });
  });

  it("TermsGate_AcceptThenAScheduledVersion_AsksForTheNextVersionWithAnEmptyBox", async () => {
    const next = {
      ...december,
      id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e30",
      effectiveDate: "2027-01-01",
      summary: null,
    };
    server.use(
      getGetMyTermsMockHandler(asks()),
      getAcceptTermsVersionMockHandler(asks(next)),
    );
    const user = userEvent.setup();
    renderGate();

    await user.click(await screen.findByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: "Accept" }));

    expect(
      await screen.findByText(/apply from 1 Jan 2027/),
    ).toBeInTheDocument();
    expect(screen.getByRole("checkbox")).not.toBeChecked();
    expect(screen.getByRole("button", { name: "Accept" })).toBeDisabled();
  });

  it("TermsGate_AcceptFailsOnTheConnection_SaysItCouldNotSave", async () => {
    server.use(
      getGetMyTermsMockHandler(asks()),
      http.post("*/v1/me/terms/versions/:versionId/accept", () =>
        HttpResponse.error(),
      ),
    );
    const user = userEvent.setup();
    renderGate();

    await user.click(await screen.findByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: "Accept" }));

    // Frame 1.12: the error above the title; the box stays ticked, so Accept tries again.
    expect(
      await screen.findByText("Couldn’t save your acceptance"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Check your connection and try again."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Accept" })).toBeEnabled();
  });

  it("TermsGate_VersionChanged_SaysSoAndAsksForTheNewLink", async () => {
    const changed = { ...december, url: "https://starter.example/terms/v2" };
    // The version's link changes when the person accepts: the API refuses the old link, and the next read gives the new one.
    const mine = { now: asks() };
    server.use(
      getGetMyTermsMockHandler(() => mine.now),
      http.post("*/v1/me/terms/versions/:versionId/accept", () => {
        mine.now = asks(changed);
        return HttpResponse.json(
          {
            status: 409,
            title: "Conflict",
            code: ErrorCode.TermsVersionChanged,
            traceId: "00-trace-01",
          },
          { status: 409 },
        );
      }),
    );
    const user = userEvent.setup();
    renderGate();

    await user.click(await screen.findByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: "Accept" }));

    expect(
      await screen.findByText(
        "The terms changed since you opened them. Read them again, then accept.",
      ),
    ).toBeInTheDocument();
    await vi.waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Read the terms ↗" }),
      ).toHaveAttribute("href", changed.url),
    );
    expect(screen.getByRole("checkbox")).not.toBeChecked();
  });

  it("TermsGate_SignOut_SignsOutToSignIn", async () => {
    server.use(getGetMyTermsMockHandler(asks()));
    const user = userEvent.setup();
    renderGate();

    await user.click(await screen.findByRole("button", { name: "Sign out" }));

    expect(signOut).toHaveBeenCalledWith({ redirectUrl: "/sign-in" });
  });

  it("TermsGate_Arabic_WritesTheDayInArabicWithWesternDigits", async () => {
    server.use(getGetMyTermsMockHandler(asks()));

    renderGate("ar");

    expect(
      await screen.findByRole("heading", { name: "تم تحديث شروط الاستخدام" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/1 ديسمبر 2026/)).toBeInTheDocument();
  });
});
