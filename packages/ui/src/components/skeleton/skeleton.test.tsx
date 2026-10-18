import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Skeleton } from "./skeleton";

describe("Skeleton", () => {
  it("Skeleton_FirstLoad_IsABusyStatusWithItsLabel", () => {
    render(<Skeleton rows={3} label="Loading members" />);

    expect(
      screen.getByRole("status", { name: "Loading members" }),
    ).toHaveAttribute("aria-busy", "true");
  });

  it("Skeleton_InRtl_Renders", () => {
    render(
      <div dir="rtl">
        <Skeleton label="جارٍ التحميل" />
      </div>,
    );

    expect(
      screen.getByRole("status", { name: "جارٍ التحميل" }),
    ).toBeInTheDocument();
  });
});
