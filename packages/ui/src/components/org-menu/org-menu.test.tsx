import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { OrgMenu } from "./org-menu";

const props = {
  name: "Northwind Traders",
  slug: "northwind",
  initials: "N",
  memberRole: "Owner",
  switchLabel: "Switch organisation",
  switchHref: "/",
};

describe("OrgMenu", () => {
  it("OrgMenu_Open_ShowsTheRoleAndTheSwitchAction", async () => {
    render(<OrgMenu {...props} />);

    await userEvent.click(
      screen.getByRole("button", { name: "Northwind Traders" }),
    );

    expect(
      await screen.findByRole("menuitem", { name: "Switch organisation" }),
    ).toHaveAttribute("href", "/");
    expect(screen.getByText("Owner")).toBeInTheDocument();
  });

  // The Tag README: a role stays plain text, never a badge. The OrgMenu draws it muted.
  it("OrgMenu_Open_ShowsTheRoleAsMutedText", async () => {
    render(<OrgMenu {...props} />);

    await userEvent.click(
      screen.getByRole("button", { name: "Northwind Traders" }),
    );

    const role = await screen.findByText("Owner");
    expect(role).toHaveClass("text-muted-foreground");
    expect(role.className).not.toMatch(/\b(bg-|border|rounded)/);
  });

  it("OrgMenu_InRtl_KeepsTheSlugLeftToRight", async () => {
    render(
      <div dir="rtl">
        <OrgMenu
          {...props}
          memberRole="مالك"
          switchLabel="تبديل المؤسسة"
          open
        />
      </div>,
    );

    expect(await screen.findByText("northwind")).toHaveAttribute("dir", "ltr");
  });
});
