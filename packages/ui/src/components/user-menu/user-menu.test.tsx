import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { describe, expect, it, vi } from "vitest";
import { UserMenu } from "./user-menu";

const props = {
  name: "Ayşe Yılmaz",
  email: "ayse@northwind.io",
  initials: "AY",
  label: "Account",
  languageName: "English",
  languageLabel: "Language",
  preferencesHref: "/northwind/settings/preferences",
  theme: "system",
  themes: [
    { id: "light", label: "Light" },
    { id: "dark", label: "Dark" },
    { id: "system", label: "System" },
  ],
  themeLabel: "Theme",
  signOutLabel: "Sign out",
};

describe("UserMenu", () => {
  // The axe engine of the apps' Playwright check (F37). jsdom draws nothing, so the rules of colour and of page regions stay out.
  // Base UI's focus guards are left out too: jsdom counts them as focusable inside aria-hidden, and Chromium's axe does not.
  it("UserMenu_Open_HasNoAxeViolation", async () => {
    render(<UserMenu {...props} onThemeChange={vi.fn()} onSignOut={vi.fn()} />);

    await userEvent.click(screen.getByRole("button", { name: /Account/ }));
    await screen.findByRole("menu");
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

  it("UserMenu_PickATheme_SavesItOnTheDevice", async () => {
    const onThemeChange = vi.fn();
    render(
      <UserMenu {...props} onThemeChange={onThemeChange} onSignOut={vi.fn()} />,
    );

    await userEvent.click(screen.getByRole("button", { name: /Account/ }));
    await userEvent.click(
      await screen.findByRole("menuitemradio", { name: "Dark" }),
    );

    expect(onThemeChange).toHaveBeenCalledWith("dark");
  });

  it("UserMenu_SignOut_IsTheLastItem", async () => {
    const onSignOut = vi.fn();
    render(
      <UserMenu {...props} onThemeChange={vi.fn()} onSignOut={onSignOut} />,
    );

    await userEvent.click(screen.getByRole("button", { name: /Account/ }));
    const items = await screen.findAllByRole("menuitem");
    await userEvent.click(items[items.length - 1] as HTMLElement);

    expect(onSignOut).toHaveBeenCalledOnce();
  });

  it("UserMenu_InRtl_KeepsTheEmailLeftToRight", async () => {
    render(
      <div dir="rtl">
        <UserMenu
          {...props}
          languageName="العربية"
          open
          onThemeChange={vi.fn()}
          onSignOut={vi.fn()}
        />
      </div>,
    );

    expect(await screen.findByText("ayse@northwind.io")).toHaveAttribute(
      "dir",
      "ltr",
    );
  });
});
