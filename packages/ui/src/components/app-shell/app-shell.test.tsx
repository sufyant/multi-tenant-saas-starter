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
  railToggle: "Menu labels",
  railShortcut: "⌘B",
};

const props = {
  nav,
  current: "settings",
  logo: "S",
  logoLabel: "Home",
  logoHref: "/northwind",
  crumbs: [
    { label: "Northwind Traders" },
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
    expect(crumbs[0]).toHaveTextContent("Northwind Traders");
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

  it("AppShell_NoCookie_ShowsTheIconRail", () => {
    render(
      <AppShell {...props} onNavigate={vi.fn()}>
        Content
      </AppShell>,
    );

    expect(screen.getByRole("button", { name: "Menu labels" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
  });

  it("AppShell_ExpandedFromTheServer_StartsExpanded", () => {
    render(
      <AppShell {...props} railExpanded onNavigate={vi.fn()}>
        Content
      </AppShell>,
    );

    expect(screen.getByRole("button", { name: "Menu labels" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
  });

  it("AppShell_Toggle_ExpandsTheRailAndSavesTheCookie", async () => {
    render(
      <AppShell {...props} onNavigate={vi.fn()}>
        Content
      </AppShell>,
    );

    await userEvent.click(screen.getByRole("button", { name: "Menu labels" }));

    expect(screen.getByRole("button", { name: "Menu labels" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
    expect(document.cookie).toContain("sidebar_state=true");
  });

  it("AppShell_CommandB_TogglesTheRail", async () => {
    render(
      <AppShell {...props} railExpanded onNavigate={vi.fn()}>
        Content
      </AppShell>,
    );

    await userEvent.keyboard("{Meta>}b{/Meta}");

    expect(screen.getByRole("button", { name: "Menu labels" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
    expect(document.cookie).toContain("sidebar_state=false");
  });

  it("AppShell_ControlB_TogglesTheRail", async () => {
    render(
      <AppShell {...props} onNavigate={vi.fn()}>
        Content
      </AppShell>,
    );

    await userEvent.keyboard("{Control>}b{/Control}");

    expect(screen.getByRole("button", { name: "Menu labels" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
  });

  it("AppShell_CommandBInATextField_LeavesTheRail", async () => {
    render(
      <AppShell {...props} onNavigate={vi.fn()}>
        <input aria-label="Notes" />
      </AppShell>,
    );

    await userEvent.click(screen.getByRole("textbox", { name: "Notes" }));
    await userEvent.keyboard("{Meta>}b{/Meta}");

    expect(screen.getByRole("button", { name: "Menu labels" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
  });

  it("AppShell_Expanded_ShowsTheLabelsInTheRail", () => {
    render(
      <AppShell {...props} railExpanded onNavigate={vi.fn()}>
        Content
      </AppShell>,
    );

    const rail = screen.getByRole("navigation", { name: "Main" });
    expect(within(rail).getByText("Overview")).toBeInTheDocument();
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
