import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Checkbox } from "./checkbox";

function renderBox(props: Parameters<typeof Checkbox>[0] = {}) {
  render(
    <>
      <Checkbox id="box" aria-describedby="box-reason" {...props} />
      <label htmlFor="box">See Activity</label>
      <p id="box-reason">Your role doesn’t have this permission.</p>
    </>,
  );
  return screen.getByRole("checkbox", { name: "See Activity" });
}

describe("Checkbox", () => {
  it("Checkbox_Click_ChecksIt", async () => {
    const onCheckedChange = vi.fn();
    const box = renderBox({ onCheckedChange });

    await userEvent.click(box);

    expect(box).toBeChecked();
    expect(onCheckedChange).toHaveBeenCalledWith(true, expect.anything());
  });

  it("Checkbox_ClickOnItsLabel_ChecksIt", async () => {
    const box = renderBox();

    await userEvent.click(screen.getByText("See Activity"));

    expect(box).toBeChecked();
  });

  it("Checkbox_Space_TogglesIt", async () => {
    const box = renderBox({ defaultChecked: true });

    await userEvent.tab();
    await userEvent.keyboard(" ");

    expect(box).toHaveFocus();
    expect(box).not.toBeChecked();
  });

  it("Checkbox_DisabledWithAReason_IsDisabledAndDescribed", async () => {
    const onCheckedChange = vi.fn();
    const box = renderBox({ disabled: true, onCheckedChange });

    await userEvent.click(box);

    expect(box).toBeDisabled();
    expect(box).not.toBeChecked();
    expect(box).toHaveAccessibleDescription(
      "Your role doesn’t have this permission.",
    );
    expect(onCheckedChange).not.toHaveBeenCalled();
  });

  it("Checkbox_Locked_StaysCheckedAndDisabled", async () => {
    const onCheckedChange = vi.fn();
    const box = renderBox({ checked: true, disabled: true, onCheckedChange });

    await userEvent.click(box);

    expect(box).toBeChecked();
    expect(box).toBeDisabled();
    expect(onCheckedChange).not.toHaveBeenCalled();
  });
});
