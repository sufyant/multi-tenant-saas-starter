import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { RoleFilter } from "./role-filter";

const options = [
  { id: "all", label: "All roles" },
  { id: "owner", label: "Owner" },
  { id: "admin", label: "Admin" },
];

const renderFilter = (value = "all", onValueChange = vi.fn()) =>
  render(
    <RoleFilter
      label="Filter by role"
      phoneLabel="Filter"
      value={value}
      options={options}
      onValueChange={onValueChange}
    />,
  );

describe("RoleFilter", () => {
  // Role filter (425:2592): a button with the chosen role, "All roles" at first; its name says what it filters.
  it("RoleFilter_Closed_ShowsTheChosenRole", () => {
    renderFilter("admin");

    expect(
      screen.getByRole("button", { name: "Filter by role: Admin" }),
    ).toHaveTextContent("Admin");
  });

  // Role filter · Menu (425:2630): a menu, not a Combobox: the list is short and needs no typing.
  it("RoleFilter_Open_ListsEveryRoleWithoutASearchField", async () => {
    renderFilter();

    await userEvent.click(
      screen.getByRole("button", { name: "Filter by role: All roles" }),
    );

    expect(
      (await screen.findAllByRole("menuitemradio")).map(
        (option) => option.textContent,
      ),
    ).toEqual(["All roles", "Owner", "Admin"]);
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
  });

  it("RoleFilter_Open_ChecksTheChosenRole", async () => {
    renderFilter("owner");

    await userEvent.click(
      screen.getByRole("button", { name: "Filter by role: Owner" }),
    );

    expect(
      await screen.findByRole("menuitemradio", { name: "Owner" }),
    ).toHaveAttribute("aria-checked", "true");
  });

  it("RoleFilter_ChooseARole_ReportsItsId", async () => {
    const onValueChange = vi.fn();
    renderFilter("all", onValueChange);

    await userEvent.click(
      screen.getByRole("button", { name: "Filter by role: All roles" }),
    );
    await userEvent.click(
      await screen.findByRole("menuitemradio", { name: "Admin" }),
    );

    expect(onValueChange).toHaveBeenCalledWith("admin");
  });

  it("RoleFilter_InRtl_HasItsName", () => {
    render(
      <div dir="rtl">
        <RoleFilter
          label="التصفية حسب الدور"
          phoneLabel="تصفية"
          value="all"
          options={[{ id: "all", label: "كل الأدوار" }]}
          onValueChange={vi.fn()}
        />
      </div>,
    );

    expect(
      screen.getByRole("button", { name: "التصفية حسب الدور: كل الأدوار" }),
    ).toBeInTheDocument();
  });
});
