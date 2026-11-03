import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Label } from "./label";

describe("Label", () => {
  it("Label_Role_IsOutlinedWithoutColour", () => {
    render(<Label>Owner</Label>);

    expect(screen.getByText("Owner")).toHaveClass(
      "inset-ring-border",
      "text-foreground",
      "whitespace-nowrap",
    );
  });
});
