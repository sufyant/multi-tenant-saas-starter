import { ErrorCode } from "@repo/api-client";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http } from "msw";
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
import { flagsAnswer, flagsList } from "../../../test/feature-flags";
import { meAnswer, problem } from "../../../test/organisations";
import { renderWithProviders } from "../../../test/render";
import { FlagsPage } from "./flags-page";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  vi.useRealTimers();
  server.resetHandlers();
  window.history.replaceState(null, "", "/");
});
afterAll(() => server.close());

const rowOf = async (key: string) => {
  const table = await screen.findByRole("table", { name: "Feature flags" });
  const link = within(table).getByRole("link", { name: key });
  return within(link.closest("tr") as HTMLElement);
};

describe("FlagsPage", () => {
  it("FlagsPage_All_ShowsEachFlagWithItsTypeStateCleanupAndLastChange", async () => {
    server.use(meAnswer(), flagsAnswer());

    renderWithProviders(<FlagsPage />);

    const bulkEdit = await rowOf("roles.bulk_edit");
    expect(bulkEdit.getByText("Release")).toBeVisible();
    expect(bulkEdit.getByText("10% · 1 organisation")).toBeVisible();
    expect(bulkEdit.getByText("28 Oct 2026 · Deniz Kaya")).toBeVisible();
    const emails = await rowOf("emails.send");
    expect(emails.getByText("Kill switch")).toBeVisible();
    expect(emails.getByText("On for everyone")).toBeVisible();
    expect(emails.getByText("12 Jan 2026")).toBeVisible();
    expect(
      (await rowOf("activity.export_v2")).getByText("Ready to remove"),
    ).toBeVisible();
    expect(
      (await rowOf("notifications.digest")).getByText("Unused"),
    ).toBeVisible();
    expect(
      screen.queryByRole("link", { name: "invitations.csv_import" }),
    ).toBeNull();
    expect(
      bulkEdit.getByRole("link", { name: "roles.bulk_edit" }),
    ).toHaveAttribute("href", "/settings/feature-flags/roles.bulk_edit");
  });

  it("FlagsPage_Tabs_CountWhatTheApiCounts", async () => {
    server.use(meAnswer(), flagsAnswer());

    renderWithProviders(<FlagsPage />);

    const tabs = within(
      await screen.findByRole("tablist", { name: "Feature flags" }),
    );
    expect(tabs.getAllByRole("tab").map((tab) => tab.textContent)).toEqual([
      "All 4",
      "Ready to remove 1",
      "Unused 1",
      "Removed from code 1",
    ]);
  });

  it("FlagsPage_RemovedTab_ShowsOnlyTheRemovedFlagWithItsDayAndTheText", async () => {
    server.use(meAnswer(), flagsAnswer());
    renderWithProviders(<FlagsPage />);

    await userEvent.click(
      await screen.findByRole("tab", { name: /Removed from code/ }),
    );

    const csv = await rowOf("invitations.csv_import");
    expect(csv.getByText("Removed")).toBeVisible();
    expect(csv.getByText("20 Oct 2026")).toBeVisible();
    expect(screen.queryByRole("link", { name: "roles.bulk_edit" })).toBeNull();
    expect(
      screen.getByText(
        "A flag removed from the code is archived here. Its name is never used again.",
      ),
    ).toBeVisible();
    expect(window.location.search).toBe("?tab=removed");
  });

  it("FlagsPage_NoFlags_SaysNoFeatureFlagsYet", async () => {
    server.use(
      meAnswer(),
      flagsAnswer({
        ...flagsList,
        flags: [],
        counts: { all: 0, readyToRemove: 0, unused: 0, removedFromCode: 0 },
      }),
    );

    renderWithProviders(<FlagsPage />);

    expect(await screen.findByText("No feature flags yet")).toBeVisible();
    expect(
      screen.getByText(
        "Flags are defined in code. They show here after the next deploy.",
      ),
    ).toBeVisible();
  });

  it("FlagsPage_ReadFails_SaysItCouldNotLoadWithTryAgain", async () => {
    server.use(
      meAnswer(),
      http.get("*/v1/platform/feature-flags", () =>
        problem(500, ErrorCode.ServerError),
      ),
    );

    renderWithProviders(<FlagsPage />);

    expect(
      await screen.findByText("Couldn’t load the feature flags"),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Try again" })).toBeVisible();
  });
  // The phone's card says how long an unused flag has been off (12.19).
  it("FlagsPage_PhoneCardOfAnUnusedFlag_SaysHowManyDaysItIsOff", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-11-02T10:00:00Z"));
    server.use(meAnswer(), flagsAnswer());

    renderWithProviders(<FlagsPage />);

    const cards = within(
      (
        await screen.findAllByRole("list", { name: "Feature flags" })
      )[0] as HTMLElement,
    );
    expect(cards.getByText("Off for everyone for 34 days")).toBeInTheDocument();
    vi.useRealTimers();
  });
});
