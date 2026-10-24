import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { RadioGroup, RadioGroupItem } from "./radio-group";

function renderRoles(props: Parameters<typeof RadioGroup>[0] = {}) {
  render(
    <RadioGroup aria-label="Role" defaultValue="member" {...props}>
      {["owner", "admin", "member"].map((role) => (
        <div key={role}>
          <RadioGroupItem
            id={role}
            value={role}
            disabled={role === "owner"}
            aria-describedby={role === "owner" ? "owner-reason" : undefined}
          />
          <label htmlFor={role}>{role}</label>
        </div>
      ))}
      <p id="owner-reason">Only an Owner can give the Owner role.</p>
    </RadioGroup>,
  );
  return (name: string) => screen.getByRole("radio", { name });
}

describe("RadioGroup", () => {
  it("RadioGroup_Default_ChecksItsValue", () => {
    const radio = renderRoles();

    expect(radio("member")).toBeChecked();
    expect(radio("admin")).not.toBeChecked();
  });

  it("RadioGroup_Click_ChoosesTheItem", async () => {
    const onValueChange = vi.fn();
    const radio = renderRoles({ onValueChange });

    await userEvent.click(radio("admin"));

    expect(radio("admin")).toBeChecked();
    expect(radio("member")).not.toBeChecked();
    expect(onValueChange).toHaveBeenCalledWith("admin", expect.anything());
  });

  it("RadioGroup_ArrowKey_MovesTheChoice", async () => {
    const radio = renderRoles();

    await userEvent.tab();
    await userEvent.keyboard("{ArrowUp}");

    expect(radio("admin")).toHaveFocus();
    expect(radio("admin")).toBeChecked();
  });

  it("RadioGroup_ArrowKey_SkipsADisabledItem", async () => {
    const radio = renderRoles({ defaultValue: "admin" });

    await userEvent.tab();
    await userEvent.keyboard("{ArrowUp}");

    expect(radio("owner")).not.toBeChecked();
    expect(radio("owner")).not.toHaveFocus();
  });

  it("RadioGroup_DisabledItemWithAReason_IsDisabledAndDescribed", async () => {
    const onValueChange = vi.fn();
    const radio = renderRoles({ onValueChange });

    await userEvent.click(radio("owner"));

    expect(radio("owner")).toBeDisabled();
    expect(radio("owner")).not.toBeChecked();
    expect(radio("owner")).toHaveAccessibleDescription(
      "Only an Owner can give the Owner role.",
    );
    expect(onValueChange).not.toHaveBeenCalled();
  });
});
