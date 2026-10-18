import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Icon } from "./icon";
import { ChevronRightIcon, PlusIcon } from "./icons";

describe("Icon", () => {
  it("Icon_WithoutLabel_IsHiddenFromAssistiveTechnology", () => {
    const { container } = render(<Icon icon={PlusIcon} />);

    expect(container.querySelector("svg")).toHaveAttribute(
      "aria-hidden",
      "true",
    );
  });

  it("Icon_WithLabel_IsAnImageWithThatName", () => {
    render(<Icon icon={PlusIcon} label="Add" />);

    expect(screen.getByRole("img", { name: "Add" })).toBeInTheDocument();
  });

  it("Icon_DirectionalInRtl_Mirrors", () => {
    const { container } = render(
      <div dir="rtl">
        <Icon icon={ChevronRightIcon} directional />
      </div>,
    );

    expect(container.querySelector("svg")).toHaveClass("rtl:-scale-x-100");
  });
});
