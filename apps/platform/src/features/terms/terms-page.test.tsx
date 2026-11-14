import { ErrorCode } from "@repo/api-client";
import { screen, within } from "@testing-library/react";
import { http } from "msw";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
} from "vitest";
import { meAnswer, problem } from "../../../test/organisations";
import { renderWithProviders } from "../../../test/render";
import {
  cancelled,
  inForce,
  replaced,
  scheduled,
  termsAnswer,
} from "../../../test/terms";
import { TermsPage } from "./terms-page";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => server.use(meAnswer()));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

/** The desktop row of a version, found by its day's link. */
const rowOf = async (day: string) =>
  within(
    (
      await within(
        await screen.findByRole("table", { name: "Terms of use" }),
      ).findByRole("link", { name: day })
    ).closest("tr") as HTMLElement,
  );

describe("TermsPage", () => {
  // 11.01: each version with its state, its link and what it asks of people.
  it("TermsPage_List_ShowsEachVersionWithItsStateLinkAndAcceptance", async () => {
    server.use(termsAnswer());

    renderWithProviders(<TermsPage />);

    const first = await rowOf("1 Dec 2026");
    expect(first.getByText("Scheduled")).toBeVisible();
    expect(first.getByText("starter.example/terms/2026-12")).toBeVisible();
    expect(first.getByText("18 of 67 accepted")).toBeVisible();
    const current = await rowOf("1 Mar 2026");
    expect(current.getByText("Current")).toBeVisible();
    expect(current.getByText("Everyone accepted")).toBeVisible();
    const old = await rowOf("12 Jan 2026");
    expect(old.getByText("Replaced")).toBeVisible();
    expect(old.getByText("No notice")).toBeVisible();
    expect(
      within(screen.getByRole("table", { name: "Terms of use" })).getByRole(
        "link",
        { name: "1 Dec 2026" },
      ),
    ).toHaveAttribute("href", `/settings/terms/${scheduled.id}`);
  });

  it("TermsPage_VersionWithAnEmailAndNoAcceptance_SaysNoticeOnly", async () => {
    server.use(
      termsAnswer([{ ...replaced, sendsEmail: true }, inForce].slice(0, 1)),
    );

    renderWithProviders(<TermsPage />);

    expect((await rowOf("12 Jan 2026")).getByText("Notice only")).toBeVisible();
  });

  // 11.13: a cancelled version leaves the list.
  it("TermsPage_CancelledVersion_IsNotListed", async () => {
    server.use(termsAnswer([cancelled, inForce, replaced]));

    renderWithProviders(<TermsPage />);

    await rowOf("1 Mar 2026");
    expect(screen.queryByText("15 Dec 2026")).toBeNull();
  });

  // 11.01: Add version is disabled while a version is scheduled, and says why (F52).
  it("TermsPage_VersionScheduled_DisablesAddVersionWithItsReason", async () => {
    server.use(termsAnswer());

    renderWithProviders(<TermsPage />);

    await rowOf("1 Dec 2026");
    const add = screen.getByRole("button", { name: "Add version" });
    expect(add).toBeDisabled();
    expect(add).toHaveAccessibleDescription(
      "A version is already scheduled for 1 Dec 2026. Edit or cancel it first.",
    );
  });

  // 11.09: with nothing scheduled, Add version opens the form.
  it("TermsPage_NothingScheduled_LinksAddVersionToTheForm", async () => {
    server.use(termsAnswer([inForce, replaced]));

    renderWithProviders(<TermsPage />);

    expect(
      await screen.findByRole("link", { name: "Add version" }),
    ).toHaveAttribute("href", "/settings/terms/new");
  });

  // 11.14: on a phone each version is a card that opens its page.
  it("TermsPage_PhoneCard_OpensTheVersionWithItsShortState", async () => {
    server.use(termsAnswer());

    renderWithProviders(<TermsPage />);

    await rowOf("1 Mar 2026");
    // The table's own card list comes first; the phone's list of 11.14 is the last.
    const cards = within(
      screen
        .getAllByRole("list", { name: "Terms of use" })
        .at(-1) as HTMLElement,
    );
    const current = cards
      .getAllByRole("link")
      .find((link) => link.textContent?.includes("1 Mar 2026"));
    expect(current).toHaveAttribute("href", `/settings/terms/${inForce.id}`);
    expect(within(current as HTMLElement).getByText("In force")).toBeVisible();
    expect(
      within(current as HTMLElement).getByText("Requires acceptance"),
    ).toBeVisible();
  });

  // 11.08: the list's error with Try again.
  it("TermsPage_ReadFails_ShowsTheErrorWithTryAgain", async () => {
    server.use(
      http.get("*/v1/platform/terms", () =>
        problem(500, ErrorCode.ServerError),
      ),
    );

    renderWithProviders(<TermsPage />);

    expect(
      await screen.findByText("Couldn’t load the terms of use"),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Try again" })).toBeVisible();
    expect(screen.getByRole("button", { name: "Add version" })).toBeDisabled();
  });
});
