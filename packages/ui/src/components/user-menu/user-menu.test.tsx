import { render, screen, within } from "@testing-library/react";
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
  it("UserMenu_Trigger_IsTheAvatarAlone", () => {
    render(<UserMenu {...props} onThemeChange={vi.fn()} onSignOut={vi.fn()} />);

    const trigger = screen.getByRole("button", { name: "AY Account" });
    expect(trigger.querySelector('[data-slot="avatar"]')).toHaveTextContent(
      "AY",
    );
    expect(trigger).toHaveClass(
      "size-control-md",
      "max-tablet:size-control-lg",
      "rounded-full",
    );
  });

  it("UserMenu_Open_ShowsTheNameThenTheEmailAndBothWrap", async () => {
    render(<UserMenu {...props} onThemeChange={vi.fn()} onSignOut={vi.fn()} />);

    await userEvent.click(screen.getByRole("button", { name: /Account/ }));
    const menu = await screen.findByRole("menu");
    const name = within(menu).getByText("Ayşe Yılmaz");
    const email = within(menu).getByText("ayse@northwind.io");
    expect(name.compareDocumentPosition(email)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
    expect(name).toHaveClass("font-medium", "text-body", "wrap-anywhere");
    expect(email).toHaveClass(
      "text-small",
      "text-muted-foreground",
      "wrap-anywhere",
    );
    expect(name.className + email.className).not.toMatch(/truncate/);
  });

  it("UserMenu_Language_IsOneRowWithTheCurrentLanguageAtItsEnd", async () => {
    render(<UserMenu {...props} onThemeChange={vi.fn()} onSignOut={vi.fn()} />);

    await userEvent.click(screen.getByRole("button", { name: /Account/ }));
    const language = await screen.findByRole("menuitem", {
      name: /^Language\s*English/,
    });
    expect(language).toHaveAttribute("href", props.preferencesHref);
    expect(within(language).getByText("English")).toHaveClass(
      "text-muted-foreground",
    );
  });

  it("UserMenu_Themes_AreThreeIconsNamedLightDarkSystem", async () => {
    render(<UserMenu {...props} onThemeChange={vi.fn()} onSignOut={vi.fn()} />);

    await userEvent.click(screen.getByRole("button", { name: /Account/ }));
    const themes = await screen.findAllByRole("menuitemradio");
    expect(themes.map((theme) => theme.getAttribute("aria-label"))).toEqual([
      "Light",
      "Dark",
      "System",
    ]);
    expect(themes.map((theme) => theme.textContent)).toEqual(["", "", ""]);
    expect(
      themes.map((theme) => theme.querySelector("svg")?.classList[1]),
    ).toEqual(["lucide-sun", "lucide-moon", "lucide-monitor"]);
    expect(themes[0]).toHaveClass("size-7", "rounded-full");
  });

  it("UserMenu_ChosenTheme_IsCheckedOnPrimarySubtle", async () => {
    render(
      <UserMenu
        {...props}
        theme="dark"
        onThemeChange={vi.fn()}
        onSignOut={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByRole("button", { name: /Account/ }));
    const dark = await screen.findByRole("menuitemradio", { name: "Dark" });
    expect(dark).toHaveAttribute("aria-checked", "true");
    expect(dark).toHaveClass(
      "data-checked:bg-primary-subtle",
      "data-checked:text-primary-subtle-foreground",
    );
    expect(
      screen.getByRole("menuitemradio", { name: "System" }),
    ).toHaveAttribute("aria-checked", "false");
  });

  it("UserMenu_PointAtATheme_ShowsItsNameAsATooltip", async () => {
    render(<UserMenu {...props} onThemeChange={vi.fn()} onSignOut={vi.fn()} />);

    await userEvent.click(screen.getByRole("button", { name: /Account/ }));
    await userEvent.hover(
      await screen.findByRole("menuitemradio", { name: "Dark" }),
    );

    const tip = await screen.findByText("Dark", {
      selector: '[data-slot="tooltip-content"]',
    });
    expect(tip).toBeVisible();
  });

  it("UserMenu_PickATheme_KeepsTheMenuOpen", async () => {
    render(<UserMenu {...props} onThemeChange={vi.fn()} onSignOut={vi.fn()} />);

    await userEvent.click(screen.getByRole("button", { name: /Account/ }));
    await userEvent.click(
      await screen.findByRole("menuitemradio", { name: "Light" }),
    );

    expect(screen.getByRole("menu")).toBeVisible();
  });

  it("UserMenu_SignOut_FollowsASeparator", async () => {
    render(<UserMenu {...props} onThemeChange={vi.fn()} onSignOut={vi.fn()} />);

    await userEvent.click(screen.getByRole("button", { name: /Account/ }));
    const signOut = await screen.findByRole("menuitem", { name: "Sign out" });

    expect(signOut.previousElementSibling).toHaveAttribute("role", "separator");
  });
});
