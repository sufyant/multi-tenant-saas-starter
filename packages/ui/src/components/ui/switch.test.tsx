import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Switch } from "./switch";

function renderSwitch(props: Parameters<typeof Switch>[0] = {}) {
  render(
    <>
      <Switch
        id="custom-roles"
        aria-describedby="custom-roles-reason"
        {...props}
      />
      <label htmlFor="custom-roles">Custom roles</label>
      <p id="custom-roles-reason">Roles an organisation creates.</p>
    </>,
  );
  return screen.getByRole("switch", { name: "Custom roles" });
}

describe("Switch", () => {
  it("Switch_Click_TurnsItOn", async () => {
    const onCheckedChange = vi.fn();
    const control = renderSwitch({ onCheckedChange });

    await userEvent.click(control);

    expect(control).toBeChecked();
    expect(onCheckedChange).toHaveBeenCalledWith(true, expect.anything());
  });

  it("Switch_ClickOnItsLabel_TurnsItOn", async () => {
    const control = renderSwitch();

    await userEvent.click(screen.getByText("Custom roles"));

    expect(control).toBeChecked();
  });

  it("Switch_Space_TogglesIt", async () => {
    const control = renderSwitch({ defaultChecked: true });

    await userEvent.tab();
    await userEvent.keyboard(" ");

    expect(control).toHaveFocus();
    expect(control).not.toBeChecked();
  });

  it("Switch_Disabled_IsDisabledAndDescribed", async () => {
    const onCheckedChange = vi.fn();
    const control = renderSwitch({ disabled: true, onCheckedChange });

    await userEvent.click(control);

    expect(control).toBeDisabled();
    expect(control).toHaveAccessibleDescription(
      "Roles an organisation creates.",
    );
    expect(onCheckedChange).not.toHaveBeenCalled();
  });

  it("Switch_On_UsesThePrimaryTokenAndItsSize", () => {
    const control = renderSwitch({ defaultChecked: true });

    expect(control).toHaveClass("h-5", "w-9", "data-checked:bg-primary");
  });
});
