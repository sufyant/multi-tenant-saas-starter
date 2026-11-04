import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { Pill } from "../pill";
import { OrgSwitcher } from "./org-switcher";

const northwind = {
  id: "1",
  name: "Northwind Traders",
  role: "Owner",
  href: "/northwind",
};
const harbour = {
  id: "2",
  name: "Blue Harbour Logistics",
  role: "Member",
  href: "/blue-harbour",
  status: <Pill tone="warning">Suspended</Pill>,
};

describe("OrgSwitcher", () => {
  it("OrgSwitcher_OneOrganisation_ShowsNameAndRoleWithoutAMenu", () => {
    render(
      <OrgSwitcher
        current={northwind}
        organisations={[northwind]}
        title="Your organisations"
      />,
    );

    expect(screen.getByText("Northwind Traders")).toBeInTheDocument();
    expect(screen.getByText("Owner")).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("OrgSwitcher_SeveralOrganisations_ListsEachWithItsRoleAndStatus", async () => {
    render(
      <OrgSwitcher
        current={northwind}
        organisations={[northwind, harbour]}
        title="Your organisations"
      />,
    );

    await userEvent.click(
      screen.getByRole("button", { name: /Northwind Traders/ }),
    );
    const menu = await screen.findByRole("menu");

    const item = within(menu).getByRole("menuitem", {
      name: /Blue Harbour Logistics/,
    });
    expect(item).toHaveAttribute("href", "/blue-harbour");
    expect(within(item).getByText("Suspended")).toBeInTheDocument();
    expect(within(item).getByText("Member")).toBeInTheDocument();
    expect(
      within(menu).getByRole("menuitem", { name: /Northwind Traders/ }),
    ).toHaveAttribute("aria-current", "page");
  });

  it("OrgSwitcher_OneOrganisationInTheDrawer_ShowsTheNameAsTheMenusHeading", () => {
    render(
      <OrgSwitcher
        current={northwind}
        organisations={[northwind]}
        title="Your organisations"
        variant="drawer"
      />,
    );

    expect(screen.getByText("Northwind Traders")).toBeInTheDocument();
    expect(screen.queryByText("Owner")).not.toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("OrgSwitcher_SeveralOrganisationsInTheDrawer_OpensTheMenu", async () => {
    render(
      <OrgSwitcher
        current={northwind}
        organisations={[northwind, harbour]}
        title="Your organisations"
        variant="drawer"
      />,
    );

    await userEvent.click(
      screen.getByRole("button", { name: /Northwind Traders/ }),
    );

    expect(await screen.findByRole("menu")).toBeInTheDocument();
  });
});
