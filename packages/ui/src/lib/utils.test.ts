import { describe, expect, it } from "vitest";
import { cn } from "./utils";

describe("cn", () => {
  // A text style of tokens.json is a font size, not a colour, so a colour after it keeps it.
  it("Cn_TextStyleThenTextColour_KeepsBoth", () => {
    expect(cn("text-small", "text-neutral-text")).toBe(
      "text-small text-neutral-text",
    );
  });

  it("Cn_TwoTextStyles_KeepsTheLater", () => {
    expect(cn("text-small", "text-body")).toBe("text-body");
  });
});
