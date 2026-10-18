import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { RowMenu } from "./row-menu";

const reason = "An organisation needs at least one Owner.";

describe("RowMenu", () => {
  it("RowMenu_DisabledItem_StaysAndShowsItsReason", async () => {
    render(
      <RowMenu
        label="Actions for Ayşe Yılmaz"
        items={[
          { label: "Change role" },
          { separator: true },
          {
            label: "Remove from organisation",
            tone: "destructive",
            disabled: true,
            reason,
          },
        ]}
      />,
    );

    await userEvent.click(
      screen.getByRole("button", { name: "Actions for Ayşe Yılmaz" }),
    );

    const item = await screen.findByRole("menuitem", {
      name: /Remove from organisation/,
    });
    expect(item).toHaveAttribute("aria-disabled", "true");
    expect(item).toHaveTextContent(reason);
  });

  it("RowMenu_SelectAnItem_CallsItsAction", async () => {
    const onSelect = vi.fn();
    render(
      <RowMenu
        label="Actions for Ayşe Yılmaz"
        items={[{ label: "Change role", onSelect }]}
      />,
    );

    await userEvent.click(
      screen.getByRole("button", { name: "Actions for Ayşe Yılmaz" }),
    );
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Change role" }),
    );

    expect(onSelect).toHaveBeenCalledOnce();
  });

  it("RowMenu_InRtl_HasItsLabel", () => {
    render(
      <div dir="rtl">
        <RowMenu label="إجراءات عائشة" items={[{ label: "تغيير الدور" }]} />
      </div>,
    );

    expect(
      screen.getByRole("button", { name: "إجراءات عائشة" }),
    ).toBeInTheDocument();
  });
});
