import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
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

  it("Banner_Dismissible_CallsOnCloseFromItsCloseButton", async () => {
    const onClose = vi.fn();
    render(
      <Banner title="Terms of use updated" onClose={onClose} closeLabel="Close">
        The new terms apply from 1 Dec 2026.
      </Banner>,
    );

    await userEvent.click(screen.getByRole("button", { name: "Close" }));

    expect(onClose).toHaveBeenCalledOnce();
  });

  it("Banner_NotDismissible_HasNoCloseButton", () => {
    render(<Banner title="Atlas Logistics is suspended." />);

    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});
