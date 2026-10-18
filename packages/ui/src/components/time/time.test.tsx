import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { Time } from "./time";

const at = "2026-10-16T13:32:00Z";

describe("Time", () => {
  it("Time_Datetime_ShowsLocalTimeOnly", () => {
    render(<Time at={at} locale="en-IE" timeZone="Europe/Dublin" />);

    expect(screen.getByText("16 Oct 2026, 14:32")).toHaveAttribute(
      "datetime",
      "2026-10-16T13:32:00.000Z",
    );
  });

  it("Time_Focus_ShowsTheZoneAndUtcInTheTooltip", async () => {
    render(<Time at={at} locale="en-IE" timeZone="Europe/Dublin" />);

    await userEvent.tab();

    expect(await screen.findByText("13:32 UTC")).toBeInTheDocument();
    expect(screen.getByText(/\(UTC\+1\)$/)).toBeInTheDocument();
  });

  it("Time_Relative_SaysHowLongAgo", () => {
    render(
      <Time
        at={at}
        now="2026-10-16T13:44:00Z"
        show="relative"
        locale="en-IE"
        timeZone="Europe/Dublin"
      />,
    );

    expect(screen.getByText("12 minutes ago")).toBeInTheDocument();
  });

  it("Time_InRtl_UsesWesternDigits", () => {
    render(
      <div dir="rtl">
        <Time at={at} show="time" locale="ar-SA" timeZone="Asia/Riyadh" />
      </div>,
    );

    expect(screen.getByText(/04:32/)).toBeInTheDocument();
  });
});
