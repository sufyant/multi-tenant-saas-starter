import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { HomeIcon, SettingsIcon } from "../icon";
import { AppShell } from "./app-shell";

const nav = [
  { id: "overview", label: "Overview", icon: HomeIcon, href: "/northwind" },
  {
    id: "settings",
    label: "Settings",
    icon: SettingsIcon,
    href: "/northwind/settings",
    placement: "bottom" as const,
  },
];

const labels = {
  navigation: "Main",
  menu: "Open menu",
  search: "Search",
  searchShortcut: "⌘K",
  breadcrumb: "Breadcrumb",
  commandTitle: "Go to",
  commandPlaceholder: "Search pages",
  commandEmpty: "No page found",
  close: "Close",
};

const props = {
  nav,
  current: "settings",
  logo: "S",
  logoLabel: "Home",
  logoHref: "/northwind",
  crumbs: [
    { label: "Northwind MRO" },
    { label: "Settings" },
    { label: "Preferences" },
  ],
  userMenu: <span>AY</span>,
  labels,
};

describe("AppShell", () => {
  it("AppShell_CurrentSection_IsThePageInTheRail", () => {
    render(
      <AppShell {...props} onNavigate={vi.fn()}>
        Content
      </AppShell>,
    );

    const rail = screen.getByRole("navigation", { name: "Main" });
    expect(
      within(rail).getByRole("link", { name: "Settings" }),
    ).toHaveAttribute("aria-current", "page");
  });

  it("AppShell_Breadcrumb_StartsWithTheOrganisation", () => {
    render(
      <AppShell {...props} onNavigate={vi.fn()}>
        Content
      </AppShell>,
    );

    const crumbs = within(
      screen.getByRole("navigation", { name: "Breadcrumb" }),
    ).getAllByRole("listitem");
    expect(crumbs[0]).toHaveTextContent("Northwind MRO");
  });

  it("AppShell_CommandK_OpensThePaletteWithTheNavigation", async () => {
    const onNavigate = vi.fn();
    render(
      <AppShell {...props} onNavigate={onNavigate}>
        Content
      </AppShell>,
    );

    await userEvent.keyboard("{Meta>}k{/Meta}");
    await userEvent.click(
      await screen.findByRole("option", { name: "Overview" }),
    );

    expect(onNavigate).toHaveBeenCalledWith("/northwind");
  });

  it("AppShell_PlatformLabel_StartsTheTopBar", () => {
    render(
      <AppShell
        {...props}
        crumbs={[{ label: "Organisations" }]}
        platformLabel="Platform"
        onNavigate={vi.fn()}
      >
        Content
      </AppShell>,
    );

    expect(screen.getByRole("banner")).toHaveTextContent(/^Platform/);
  });

  it("AppShell_InRtl_Renders", () => {
    render(
      <div dir="rtl">
        <AppShell
          {...props}
          labels={{ ...labels, navigation: "الرئيسية" }}
          onNavigate={vi.fn()}
        >
          المحتوى
        </AppShell>
      </div>,
    );

    expect(
      screen.getByRole("navigation", { name: "الرئيسية" }),
    ).toBeInTheDocument();
  });
});
