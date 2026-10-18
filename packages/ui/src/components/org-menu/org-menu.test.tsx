import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { OrgMenu } from "./org-menu";

const props = {
  name: "Northwind MRO",
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
      screen.getByRole("button", { name: "Northwind MRO" }),
    );

    expect(
      await screen.findByRole("menuitem", { name: "Switch organisation" }),
    ).toHaveAttribute("href", "/");
    expect(screen.getByText("Owner")).toBeInTheDocument();
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
