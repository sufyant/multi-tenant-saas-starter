import { type MyTermsResponse, MyTermsResponseAction } from "@repo/api-client";
import {
  getDismissTermsNoticeMockHandler,
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
import { TermsNotice } from "./terms-notice";

const december = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
  effectiveDate: "2026-12-01",
  url: "https://starter.example/terms/2026-12",
  summary: "We now keep exported data files for 7 days.",
  requiresAcceptance: false,
};
const notice: MyTermsResponse = {
  action: MyTermsResponseAction.Notice,
  version: december,
};
const nothing: MyTermsResponse = {
  action: MyTermsResponseAction.Nothing,
  version: null,
};

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe("TermsNotice", () => {
  it("TermsNotice_Notice_ShowsTheDayTheLinkAndNoSummary", async () => {
    server.use(getGetMyTermsMockHandler(notice));

    renderWithProviders(<TermsNotice />);

    // Frame 4.03: the title, the day and the email, Read the terms, and the close button.
    const banner = await screen.findByRole("status");
    expect(banner).toHaveTextContent(
      "Terms of use updated The new terms apply from 1 Dec 2026. We also sent you an email.",
    );
    expect(banner).not.toHaveTextContent(december.summary);
    expect(
      screen.getByRole("button", { name: "Read the terms ↗" }),
    ).toHaveAttribute("href", december.url);
    expect(screen.getByRole("button", { name: "Close" })).toBeInTheDocument();
  });

  it("TermsNotice_Close_DismissesTheVersionAndHides", async () => {
    const dismissed = vi.fn();
    server.use(
      getGetMyTermsMockHandler(notice),
      getDismissTermsNoticeMockHandler(({ params }) => {
        dismissed(params.versionId);
        return nothing;
      }),
    );
    const user = userEvent.setup();
    renderWithProviders(<TermsNotice />);

    await user.click(await screen.findByRole("button", { name: "Close" }));

    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    await vi.waitFor(() => expect(dismissed).toHaveBeenCalledWith(december.id));
  });

  it("TermsNotice_CloseFails_ShowsTheNoticeAgain", async () => {
    server.use(
      getGetMyTermsMockHandler(notice),
      http.post("*/v1/me/terms/versions/:versionId/dismiss", () =>
        HttpResponse.error(),
      ),
    );
    const user = userEvent.setup();
    renderWithProviders(<TermsNotice />);

    await user.click(await screen.findByRole("button", { name: "Close" }));

    expect(await screen.findByRole("status")).toBeInTheDocument();
  });

  it("TermsNotice_Nothing_ShowsNothing", async () => {
    const read = vi.fn();
    server.use(
      getGetMyTermsMockHandler(() => {
        read();
        return nothing;
      }),
    );

    renderWithProviders(<TermsNotice />);

    await vi.waitFor(() => expect(read).toHaveBeenCalled());
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });
});
