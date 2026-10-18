import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Status } from "./status";

describe("Status", () => {
  it("Status_Warning_ShowsTheWord", () => {
    render(<Status tone="warning">Suspended</Status>);

    expect(screen.getByText("Suspended")).toHaveClass("text-warning-text");
  });

  it("Status_InRtl_ShowsTheWord", () => {
    render(
      <div dir="rtl">
        <Status tone="success">نشط</Status>
      </div>,
    );

    expect(screen.getByText("نشط")).toBeInTheDocument();
  });
});
