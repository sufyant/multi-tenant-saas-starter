import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Pill } from "./pill";

describe("Pill", () => {
  it.each([
    ["success", "text-success-text"],
    ["warning", "text-warning-text"],
    ["info", "text-info-text"],
    ["destructive", "text-destructive-text"],
    ["neutral", "text-neutral-text"],
  ] as const)("Pill_Tone%s_UsesItsTextColour", (tone, colour) => {
    render(<Pill tone={tone}>Word</Pill>);

    expect(screen.getByText("Word")).toHaveClass(colour, "whitespace-nowrap");
  });
});
