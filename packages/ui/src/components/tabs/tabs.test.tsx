import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Tabs } from "./tabs";

const items = [
  { id: "members", label: "Members", count: 6 },
  { id: "invitations", label: "Invitations", count: 3 },
];

describe("Tabs", () => {
  it("Tabs_Current_IsSelected", () => {
    render(
      <Tabs items={items} current="members" label="Members and invitations" />,
    );

    expect(screen.getByRole("tab", { name: "Members 6" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
  });

  it("Tabs_ClickAnotherTab_ReportsItsId", async () => {
    const onValueChange = vi.fn();
    render(
      <Tabs
        items={items}
        current="members"
        label="Members and invitations"
        onValueChange={onValueChange}
      />,
    );

    await userEvent.click(screen.getByRole("tab", { name: "Invitations 3" }));

    expect(onValueChange).toHaveBeenCalledWith("invitations");
  });

  it("Tabs_InRtl_Renders", () => {
    render(
      <div dir="rtl">
        <Tabs
          items={[{ id: "members", label: "الأعضاء" }]}
          current="members"
          label="الأعضاء"
        />
      </div>,
    );

    expect(
      screen.getByRole("tablist", { name: "الأعضاء" }),
    ).toBeInTheDocument();
  });
});
