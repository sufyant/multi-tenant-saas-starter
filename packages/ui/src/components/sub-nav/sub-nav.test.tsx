import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SubNav } from "./sub-nav";

const groups = [
  {
    label: "Organisation",
    items: [
      { id: "general", label: "General", href: "/northwind/settings/general" },
      { id: "members", label: "Members", href: "/northwind/settings/members" },
    ],
  },
  {
    label: "You",
    items: [
      {
        id: "preferences",
        label: "Preferences",
        href: "/northwind/settings/preferences",
      },
    ],
  },
];

describe("SubNav", () => {
  it("SubNav_CurrentItem_IsThePage", () => {
    render(<SubNav groups={groups} current="members" label="Settings" />);

    expect(screen.getByRole("link", { name: "Members" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(screen.getByRole("link", { name: "General" })).not.toHaveAttribute(
      "aria-current",
    );
  });

  it("SubNav_InRtl_Renders", () => {
    render(
      <div dir="rtl">
        <SubNav
          groups={[
            { items: [{ id: "preferences", label: "التفضيلات", href: "/p" }] },
          ]}
          current="preferences"
          label="الإعدادات"
        />
      </div>,
    );

    expect(
      screen.getByRole("navigation", { name: "الإعدادات" }),
    ).toBeInTheDocument();
  });
});
