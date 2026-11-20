import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Meter } from "./meter";

describe("Meter", () => {
  it("Meter_Normal_FillsThePartUsedInPrimary", () => {
    render(
      <Meter
        label="7,200 of 10,000 AI requests"
        aside="72%"
        meta="Resets on 1 Dec"
        used={7200}
        quota={10000}
      />,
    );

    const meter = screen.getByRole("meter", {
      name: "7,200 of 10,000 AI requests",
    });
    expect(meter).toHaveAttribute("aria-valuenow", "7200");
    expect(meter).toHaveAttribute("aria-valuemax", "10000");
    expect(screen.getByTestId("meter-bar")).toHaveClass("bg-primary");
    expect(screen.getByTestId("meter-bar")).toHaveStyle({ width: "72%" });
    expect(screen.getByText("Resets on 1 Dec")).toBeVisible();
  });

  it("Meter_Near_FillsInWarning", () => {
    render(
      <Meter label="8,000 of 10,000" aside="80%" used={8000} quota={10000} />,
    );

    expect(screen.getByTestId("meter-bar")).toHaveClass("bg-warning");
  });

  it("Meter_Over_ShowsTheQuotaInPrimaryAndTheExtraInWarning", () => {
    render(
      <Meter
        label="11,400 of 10,000"
        aside="1,400 extra"
        used={11400}
        quota={10000}
      />,
    );

    expect(screen.getByTestId("meter-bar")).toHaveStyle({
      width: `${(10000 / 11400) * 100}%`,
    });
    expect(screen.getByTestId("meter-extra")).toHaveClass("bg-warning");
    expect(screen.getByText("1,400 extra")).toHaveClass("text-warning-text");
  });

  it("Meter_Stopped_FillsTheTrackInDestructive", () => {
    render(
      <Meter
        label="100 of 100"
        aside="Stopped"
        used={100}
        quota={100}
        stopped
      />,
    );

    expect(screen.getByTestId("meter-bar")).toHaveClass("bg-destructive");
    expect(screen.getByTestId("meter-bar")).toHaveStyle({ width: "100%" });
    expect(screen.getByText("Stopped")).toHaveClass("text-destructive-text");
  });
});
