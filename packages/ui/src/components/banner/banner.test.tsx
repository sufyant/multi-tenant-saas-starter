import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Banner } from "./banner";

describe("Banner", () => {
  it("Banner_SuspendedTenant_StatesTheFactAndTheConsequence", () => {
    render(
      <Banner tone="warning" title="Atlas Logistics is suspended.">
        Members cannot sign in.
      </Banner>,
    );

    expect(screen.getByRole("status")).toHaveTextContent(
      "Atlas Logistics is suspended. Members cannot sign in.",
    );
  });

  it("Banner_InRtl_Renders", () => {
    render(
      <div dir="rtl">
        <Banner tone="warning" title="Atlas Logistics موقوفة." />
      </div>,
    );

    expect(screen.getByText("Atlas Logistics موقوفة.")).toBeInTheDocument();
  });
});
