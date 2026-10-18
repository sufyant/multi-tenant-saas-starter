import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Icon, PlusIcon } from "../icon";
import { Button } from "./button";

describe("Button", () => {
  it("Button_Click_CallsItsHandler", async () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick}>Export</Button>);

    await userEvent.click(screen.getByRole("button", { name: "Export" }));

    expect(onClick).toHaveBeenCalledOnce();
  });

  it("Button_WithoutAVariant_IsSecondary", () => {
    render(<Button>Export</Button>);

    expect(screen.getByRole("button", { name: "Export" })).toHaveClass(
      "bg-card",
    );
  });

  it("Button_IconOnly_IsSquareWithItsLabel", () => {
    render(
      <Button icon={<Icon icon={PlusIcon} />} aria-label="Invite member" />,
    );

    expect(screen.getByRole("button", { name: "Invite member" })).toHaveClass(
      "size-control-md",
    );
  });

  it("Button_InRtl_KeepsTheIconBeforeTheLabel", () => {
    render(
      <div dir="rtl">
        <Button variant="primary" icon={<Icon icon={PlusIcon} />}>
          دعوة عضو
        </Button>
      </div>,
    );

    expect(
      screen.getByRole("button", { name: "دعوة عضو" }).firstChild?.nodeName,
    ).toBe("svg");
  });
});
