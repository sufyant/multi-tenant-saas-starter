import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { EmptyValue } from "./empty-value";

describe("EmptyValue", () => {
  it("EmptyValue_Render_ShowsAMutedDash", () => {
    render(<EmptyValue />);

    expect(screen.getByText("—")).toHaveClass("text-muted-foreground");
  });
});
