import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { OrgTile } from "./org-tile";

describe("OrgTile", () => {
  // Every organisation's tile is the info tint; only the product's mark keeps the brand's gradient (the maintainer's decision of 2026-11-08).
  it("OrgTile_Organisation_DrawsItsFirstLetterOnTheInfoTint", () => {
    render(<OrgTile name="Northwind Traders" />);

    const tile = screen.getByText("N");
    expect(tile).toHaveClass(
      "bg-info-subtle",
      "text-info-text",
      "size-6",
      "rounded-md",
    );
    expect(tile).not.toHaveClass("bg-linear-135");
    expect(tile).toHaveAttribute("aria-hidden", "true");
  });

  // Choose an organisation draws a 32 px tile with 14 px text (1.18).
  it("OrgTile_Large_IsTheListsTile", () => {
    render(<OrgTile name="الأفق" size="lg" />);

    expect(screen.getByText("ا")).toHaveClass(
      "bg-info-subtle",
      "size-8",
      "rounded-lg",
      "text-body",
    );
  });
});
