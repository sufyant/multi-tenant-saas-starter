import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ShieldIcon, TrashIcon } from "../icon";
import { RowMenu } from "./row-menu";

const reason = "An organisation needs at least one Owner.";

describe("RowMenu", () => {
  it("RowMenu_DisabledItem_StaysAndShowsItsReason", async () => {
    render(
      <RowMenu
        label="Actions for Ayşe Yılmaz"
        items={[
          { label: "Change role" },
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

  it("RowMenu_NoAlign_OpensAlignedToTheButtonsEnd", async () => {
    render(
      <RowMenu label="Actions for Ayşe Yılmaz" items={[{ label: "Edit" }]} />,
    );

    await userEvent.click(
      screen.getByRole("button", { name: "Actions for Ayşe Yılmaz" }),
    );

    expect(await screen.findByRole("menu")).toHaveAttribute(
      "data-align",
      "end",
    );
  });

  // A page header's ⋯ opens under the button towards the end of the reading direction (8.41).
  it("RowMenu_AlignStart_OpensAlignedToTheButtonsStart", async () => {
    render(
      <RowMenu
        label="More actions"
        align="start"
        items={[{ label: "Export as CSV" }]}
      />,
    );

    await userEvent.click(screen.getByRole("button", { name: "More actions" }));

    expect(await screen.findByRole("menu")).toHaveAttribute(
      "data-align",
      "start",
    );
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

  // Safe actions first, then a separator, then the destructive group (the RowMenu component, 406:2426; Apple HIG, Menus).
  it("RowMenu_SafeAndDestructiveItems_SeparatesTheDestructiveGroup", async () => {
    render(
      <RowMenu
        label="Actions for Ayşe Yılmaz"
        items={[
          { label: "Change role" },
          { label: "Copy email" },
          { label: "Remove from organisation", tone: "destructive" },
          { label: "Block", tone: "destructive" },
        ]}
      />,
    );

    await userEvent.click(
      screen.getByRole("button", { name: "Actions for Ayşe Yılmaz" }),
    );

    const menu = await screen.findByRole("menu");
    const parts = [
      ...menu.querySelectorAll("[role=menuitem], [role=separator]"),
    ];
    expect(parts.map((part) => part.getAttribute("role"))).toEqual([
      "menuitem",
      "menuitem",
      "separator",
      "menuitem",
      "menuitem",
    ]);
  });

  it("RowMenu_OnlyDestructiveItems_HasNoSeparator", async () => {
    render(
      <RowMenu
        label="Actions for Selin Arslan"
        items={[{ label: "Remove system admin", tone: "destructive" }]}
      />,
    );

    await userEvent.click(
      screen.getByRole("button", { name: "Actions for Selin Arslan" }),
    );

    await screen.findByRole("menuitem", { name: "Remove system admin" });
    expect(screen.queryByRole("separator")).not.toBeInTheDocument();
  });

  it("RowMenu_ItemWithIcon_DrawsItsIcon", async () => {
    render(
      <RowMenu
        label="Actions for Ayşe Yılmaz"
        items={[
          { label: "Change role", icon: ShieldIcon },
          {
            label: "Remove from organisation",
            icon: TrashIcon,
            tone: "destructive",
          },
        ]}
      />,
    );

    await userEvent.click(
      screen.getByRole("button", { name: "Actions for Ayşe Yılmaz" }),
    );

    const item = await screen.findByRole("menuitem", { name: "Change role" });
    expect(item.querySelector("svg")).toHaveAttribute("width", "16");
  });
});
