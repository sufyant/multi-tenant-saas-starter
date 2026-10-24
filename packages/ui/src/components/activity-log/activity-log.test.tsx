import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ActivityLog, type ActivityLogProps } from "./activity-log";

// Only Date is fake: "now" is 16 Oct 2026, 14:32 in Dublin, the boards' moment.
beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-10-16T13:32:00Z"));
});
afterEach(() => vi.useRealTimers());

type Entry = { id: string; occurredAt: string; text: string };

const entries: Entry[] = [
  { id: "1", occurredAt: "2026-10-16T08:40:00Z", text: "Today's entry." },
  {
    id: "2",
    occurredAt: "2026-10-15T23:30:00Z",
    text: "Yesterday's late entry.",
  },
  { id: "3", occurredAt: "2026-10-01T09:00:00Z", text: "An older entry." },
];

const renderLog = (props: Partial<ActivityLogProps<Entry>> = {}) =>
  render(
    <ActivityLog
      entries={entries}
      sentence={(entry) => <p>{entry.text}</p>}
      locale="en-IE"
      timeZone="Europe/Dublin"
      labels={{
        today: (date) => `Today, ${date}`,
        yesterday: (date) => `Yesterday, ${date}`,
        older: "Load older activity",
      }}
      hasOlder={false}
      loadingOlder={false}
      onOlder={() => {}}
      {...props}
    />,
  );

/** The day headings and, under each, the text of its entries, in order. */
const days = () =>
  screen.getAllByRole("heading", { level: 2 }).map((heading) => [
    heading.textContent,
    within(heading.nextElementSibling as HTMLElement)
      .getAllByRole("listitem")
      .map((item) => item.querySelector("p")?.textContent),
  ]);

describe("ActivityLog", () => {
  it("ActivityLog_Entries_GroupsThemByDayAndNamesTodayAndYesterday", () => {
    renderLog();

    expect(days()).toEqual([
      ["Today, 16 Oct 2026", ["Today's entry."]],
      ["Yesterday, 15 Oct 2026", ["Yesterday's late entry."]],
      ["1 Oct 2026", ["An older entry."]],
    ]);
  });

  // 23:30 and 22:30 UTC on the 15th: after and before Dublin's midnight (UTC+1).
  it("ActivityLog_MidnightInTheViewersZone_PutsAnEntryOnItsLocalDay", () => {
    renderLog({
      entries: [
        {
          id: "1",
          occurredAt: "2026-10-15T23:30:00Z",
          text: "After midnight.",
        },
        {
          id: "2",
          occurredAt: "2026-10-15T22:30:00Z",
          text: "Before midnight.",
        },
      ],
    });

    expect(days()).toEqual([
      ["Today, 16 Oct 2026", ["After midnight."]],
      ["Yesterday, 15 Oct 2026", ["Before midnight."]],
    ]);
  });

  it("ActivityLog_Entry_ShowsItsLocalTime", () => {
    renderLog({ entries: [entries[0] as Entry] });

    expect(screen.getByText("09:40")).toHaveAttribute(
      "datetime",
      "2026-10-16T08:40:00.000Z",
    );
  });

  it("ActivityLog_HasOlder_TheButtonAsksForThem", async () => {
    const onOlder = vi.fn();
    renderLog({ hasOlder: true, onOlder });

    await userEvent.click(
      screen.getByRole("button", { name: "Load older activity" }),
    );

    expect(onOlder).toHaveBeenCalledOnce();
  });

  it("ActivityLog_LoadingOlder_DisablesTheButton", () => {
    renderLog({ hasOlder: true, loadingOlder: true });

    expect(
      screen.getByRole("button", { name: "Load older activity" }),
    ).toBeDisabled();
  });

  it("ActivityLog_LastPage_ShowsNoButton", () => {
    renderLog();

    expect(
      screen.queryByRole("button", { name: "Load older activity" }),
    ).not.toBeInTheDocument();
  });
});
