import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it, vi } from "vitest";
import en from "../../../messages/en.json";
import { AccountMenu, type AccountMenuProps } from "./account-menu";

const props = {
  name: "Ayşe Demir",
  email: "ayse@northwind.example",
  person: { name: "Ayşe Demir" },
  profileHref: "/northwind/settings/profile",
  version: "a1b2c3d",
};

async function openMenu(extra: Partial<AccountMenuProps> = {}) {
  const onSignOut = vi.fn();
  const user = userEvent.setup();
  render(
    <NextIntlClientProvider locale="en" messages={en}>
      <AccountMenu {...props} onSignOut={onSignOut} {...extra} />
    </NextIntlClientProvider>,
  );
  await user.click(screen.getByRole("button", { name: /Ayşe Demir/ }));
  return { menu: await screen.findByRole("menu"), onSignOut, user };
}

describe("AccountMenu", () => {
  // The axe engine of the apps' Playwright check (F37). jsdom draws nothing, so the rules of colour and of page regions stay out.
  // Base UI's focus guards are left out too: jsdom counts them as focusable inside aria-hidden, and Chromium's axe does not.
  it("AccountMenu_Open_HasNoAxeViolation", async () => {
    await openMenu();
    const result = await axe.run(
      { include: [document.body], exclude: [["[data-base-ui-focus-guard]"]] },
      {
        rules: {
          "color-contrast": { enabled: false },
          region: { enabled: false },
        },
      },
    );

    expect(result.violations.map((violation) => violation.id)).toEqual([]);
  });

  // The sidebar is an icon rail under bp-desktop and open from it, so the rail keeps the name for the open sidebar (the Sidebar's
  // description); the classes hide it under bp-desktop, which jsdom does not draw.
  it("AccountMenu_Rail_KeepsTheNameForTheOpenSidebar", () => {
    render(
      <NextIntlClientProvider locale="en" messages={en}>
        <AccountMenu {...props} onSignOut={vi.fn()} variant="rail" />
      </NextIntlClientProvider>,
    );

    expect(screen.getByText("Ayşe Demir")).toHaveClass("max-desktop:hidden");
    expect(screen.getByText("AD Account")).toHaveClass("desktop:hidden");
  });

  it("AccountMenu_Open_ShowsProfileSignOutAndTheVersionInThisOrder", async () => {
    const { menu } = await openMenu();

    expect(
      within(menu)
        .getAllByRole("menuitem")
        .map((item) => item.textContent || item.getAttribute("aria-label")),
    ).toEqual(["Profile", "Sign out", "Version a1b2c3d"]);
    expect(
      within(menu).getByRole("menuitem", { name: "Profile" }),
    ).toHaveAttribute("href", "/northwind/settings/profile");
  });

  // "No access yet" has no organisation, so no Profile to open (frame 1.19; the maintainer's decision of 2026-11-07).
  it("AccountMenu_NoProfileHref_ShowsNoProfileItem", async () => {
    const { menu } = await openMenu({ profileHref: undefined });

    expect(
      within(menu)
        .getAllByRole("menuitem")
        .map((item) => item.textContent || item.getAttribute("aria-label")),
    ).toEqual(["Sign out", "Version a1b2c3d"]);
  });

  // The account button at the top of a page without the shell (frame 1.19): a round button with the avatar and the name, no
  // chevron, and the menu under it.
  it("AccountMenu_Chip_IsARoundButtonWithTheAvatarAndTheName", () => {
    render(
      <NextIntlClientProvider locale="en" messages={en}>
        <AccountMenu {...props} onSignOut={vi.fn()} variant="chip" />
      </NextIntlClientProvider>,
    );

    const button = screen.getByRole("button", { name: /Ayşe Demir/ });
    expect(button).toHaveClass("rounded-full", "border", "h-9");
    expect(button.querySelector(".lucide-chevrons-up-down")).toBeNull();
  });

  it("AccountMenu_AccountAndSecurityHref_ShowsTheItem", async () => {
    const { menu } = await openMenu({
      accountAndSecurityHref: "/northwind/settings/account",
    });

    expect(
      within(menu).getByRole("menuitem", { name: "Account and security" }),
    ).toBeInTheDocument();
  });

  // The platform's own item (2.03): the web app in a new tab, which its name says, as WCAG's technique G201 asks.
  it("AccountMenu_WebAppHref_OpensTheWebAppInANewTab", async () => {
    const { menu } = await openMenu({
      accountAndSecurityHref: "/settings/account",
      webAppHref: "https://app.example.test",
    });

    const item = within(menu).getByRole("menuitem", {
      name: "Open the web app (opens in a new tab)",
    });
    expect(item).toHaveAttribute("href", "https://app.example.test");
    expect(item).toHaveAttribute("target", "_blank");
    expect(
      within(menu)
        .getAllByRole("menuitem")
        .map((each) => each.textContent),
    ).toEqual([
      "Profile",
      "Account and security",
      "Open the web app (opens in a new tab)",
      "Sign out",
      "Version a1b2c3d",
    ]);
  });

  it("AccountMenu_NoWebAppHref_ShowsNoWebAppItem", async () => {
    const { menu } = await openMenu();

    expect(
      within(menu).queryByRole("menuitem", { name: /Open the web app/ }),
    ).not.toBeInTheDocument();
  });

  it("AccountMenu_ClickTheVersion_CopiesItAndSaysCopied", async () => {
    const { menu, user } = await openMenu();

    await user.click(
      within(menu).getByRole("menuitem", { name: "Copy version a1b2c3d" }),
    );

    expect(await navigator.clipboard.readText()).toBe("a1b2c3d");
    await waitFor(() =>
      expect(screen.getByRole("status")).toHaveTextContent("Copied"),
    );
    expect(screen.getByRole("menu")).toBeInTheDocument();
  });

  it("AccountMenu_NoVersion_ShowsNoVersionRow", async () => {
    const { menu } = await openMenu({ version: undefined });

    expect(within(menu).queryByText(/^Version/)).not.toBeInTheDocument();
  });

  it("AccountMenu_SignOut_SignsOut", async () => {
    const { menu, onSignOut, user } = await openMenu();

    await user.click(within(menu).getByRole("menuitem", { name: "Sign out" }));

    expect(onSignOut).toHaveBeenCalledOnce();
  });
});
