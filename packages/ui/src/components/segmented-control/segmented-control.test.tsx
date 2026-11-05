import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { MonitorIcon, MoonIcon, SunIcon } from "../icon/icons";
import { SegmentedControl } from "./segmented-control";

const options = [
  { value: "system", label: "System", icon: MonitorIcon },
  { value: "light", label: "Light", icon: SunIcon },
  { value: "dark", label: "Dark", icon: MoonIcon },
];

function renderControl(
  props: Partial<Parameters<typeof SegmentedControl>[0]> = {},
) {
  const onValueChange = vi.fn();
  render(
    <SegmentedControl
      label="Theme"
      value="system"
      options={options}
      onValueChange={onValueChange}
      {...props}
    />,
  );
  return { onValueChange };
}

describe("SegmentedControl", () => {
  it("SegmentedControl_ThreeOptions_IsARadioGroupWithTheValueChecked", () => {
    renderControl();

    const group = screen.getByRole("radiogroup", { name: "Theme" });
    expect(
      [...group.querySelectorAll("[role=radio]")].map(
        (radio) => radio.textContent,
      ),
    ).toEqual(["System", "Light", "Dark"]);
    expect(screen.getByRole("radio", { name: "System" })).toBeChecked();
    expect(screen.getByRole("radio", { name: "Dark" })).not.toBeChecked();
  });

  it("SegmentedControl_ClickAnOption_ChoosesIt", async () => {
    const { onValueChange } = renderControl();

    await userEvent.click(screen.getByRole("radio", { name: "Dark" }));

    expect(onValueChange).toHaveBeenCalledWith("dark");
  });

  it("SegmentedControl_ArrowKey_MovesTheChoiceAndTheFocus", async () => {
    const { onValueChange } = renderControl();
    await userEvent.tab();

    await userEvent.keyboard("{ArrowRight}");

    expect(onValueChange).toHaveBeenCalledWith("light");
    expect(screen.getByRole("radio", { name: "Light" })).toHaveFocus();
  });

  it("SegmentedControl_Tab_StopsOnlyOnTheChosenOption", async () => {
    renderControl({ value: "light" });

    await userEvent.tab();

    expect(screen.getByRole("radio", { name: "Light" })).toHaveFocus();
  });

  it("SegmentedControl_Disabled_DisablesEveryOption", () => {
    renderControl({ disabled: true });

    expect(
      screen
        .getAllByRole("radio")
        .map((radio) => (radio as HTMLButtonElement).disabled),
    ).toEqual([true, true, true]);
  });

  it("SegmentedControl_DefaultSize_IsTheMediumControlHeight", () => {
    renderControl();

    expect(screen.getByRole("radiogroup")).toHaveClass("h-control-md");
  });

  it("SegmentedControl_TouchSize_IsTheTouchHeight", () => {
    renderControl({ size: "touch" });

    expect(screen.getByRole("radiogroup")).toHaveClass("h-control-lg");
  });

  it("SegmentedControl_AdaptiveSize_IsTouchBelowTheDesktopBreakpoint", () => {
    renderControl({ size: "adaptive" });

    expect(screen.getByRole("radiogroup")).toHaveClass(
      "h-control-lg",
      "desktop:h-control-md",
    );
  });
});
