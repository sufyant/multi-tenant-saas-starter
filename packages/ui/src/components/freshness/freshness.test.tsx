import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Freshness } from "./freshness";

const at = "2026-10-16T13:32:00Z";

describe("Freshness", () => {
  it("Freshness_Loaded_SaysUpdatedAndTheLocalTime", () => {
    render(
      <Freshness
        at={at}
        locale="en-IE"
        timeZone="Europe/Dublin"
        label="Updated"
      />,
    );

    expect(screen.getByText("Updated", { exact: false })).toHaveTextContent(
      "Updated 14:32",
    );
  });

  it("Freshness_Stale_HasAnAmberDot", () => {
    const { container } = render(
      <Freshness
        at={at}
        locale="en-IE"
        timeZone="Europe/Dublin"
        label="Updated"
        stale
      />,
    );

    expect(container.querySelector(".bg-warning")).toBeInTheDocument();
  });

  it("Freshness_InRtl_UsesWesternDigits", () => {
    render(
      <div dir="rtl">
        <Freshness
          at={at}
          locale="ar-SA"
          timeZone="Asia/Riyadh"
          label="آخر تحديث"
        />
      </div>,
    );

    expect(screen.getByText("آخر تحديث", { exact: false })).toHaveTextContent(
      /04:32/,
    );
  });
});
