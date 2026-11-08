import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { afterEach, describe, expect, it, vi } from "vitest";
import en from "../../../messages/en.json";
import {
  ActivityIcon,
  HomeIcon,
  SettingsIcon,
  UserIcon,
  UsersIcon,
} from "../icon";
import { AppShell, type AppShellProps } from "./app-shell";

const base = {
  head: () => <span>Northwind Traders</span>,
  main: [{ id: "home", label: "Home", icon: HomeIcon, href: "/northwind" }],
  settings: {
    id: "settings",
    label: "Settings",
    icon: SettingsIcon,
    href: "/northwind/settings/general",
  },
  current: "home",
  pageName: "Home",
  crumbs: [{ label: "Northwind Traders" }, { label: "Home" }],
  commands: [
    {
      id: "go-to",
      label: "Go to",
      items: [{ id: "home", label: "Home", href: "/northwind" }],
    },
  ],
};

const settingsMenu = {
  back: { label: "Back to Northwind Traders", href: "/northwind" },
  groups: [
    {
      id: "my-account",
      label: "My account",
      items: [
        {
          id: "profile",
          label: "Profile",
          icon: UserIcon,
          href: "/northwind/settings/profile",
        },
      ],
    },
    {
      id: "organisation",
      label: "Organisation",
      items: [
        {
          id: "members",
          label: "Members",
          icon: UsersIcon,
          href: "/northwind/settings/members",
          count: 11,
        },
        {
          id: "activity",
          label: "Activity log",
          icon: ActivityIcon,
          href: "/northwind/settings/activity",
        },
      ],
    },
  ],
};

function renderShell(props: Partial<AppShellProps> = {}) {
  const onNavigate = vi.fn();
  render(
    <NextIntlClientProvider locale="en" messages={en}>
      <AppShell {...base} onNavigate={onNavigate} {...props}>
        Page
      </AppShell>
    </NextIntlClientProvider>,
  );
  return onNavigate;
}

/** jsdom has no layout: the phone layout follows the breakpoint token, which the test sets and a matchMedia that never matches. */
function onAPhone() {
  document.documentElement.style.setProperty("--bp-tablet", "768px");
}

describe("AppShell", () => {
  afterEach(() => {
    document.documentElement.style.removeProperty("--bp-tablet");
    document.title = "";
  });

  it("AppShell_MainMenu_ShowsTheHeadSearchHomeAndSettingsAtTheFoot", () => {
    renderShell();
    const menu = screen.getByRole("navigation", { name: "Main menu" });

    expect(within(menu).getByText("Northwind Traders")).toBeInTheDocument();
    expect(
      within(menu).getByRole("button", { name: "Search or jump to" }),
    ).toBeInTheDocument();
    expect(
      within(menu)
        .getAllByRole("link")
        .map((link) => link.textContent),
    ).toEqual(["Home", "Settings"]);
    expect(within(menu).getByRole("link", { name: "Home" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("AppShell_SettingsMenu_ShowsBackAndItsGroupsInPlaceOfTheMainMenu", () => {
    renderShell({ settingsMenu, current: "members" });
    const menu = screen.getByRole("navigation", { name: "Settings menu" });

    expect(
      within(menu)
        .getAllByRole("link")
        .map((link) => link.textContent),
    ).toEqual([
      "Back to Northwind Traders",
      "Profile",
      "Members11",
      "Activity log",
    ]);
    expect(within(menu).getByText("My account")).toBeInTheDocument();
    expect(within(menu).getByText("Organisation")).toBeInTheDocument();
    expect(within(menu).queryByRole("link", { name: "Home" })).toBeNull();
  });

  it("AppShell_Bell_SitsInTheSidebarFromTablet", () => {
    renderShell({
      bell: (place) => <button type="button">{`bell ${place}`}</button>,
    });

    expect(
      within(screen.getByRole("navigation", { name: "Main menu" })).getByRole(
        "button",
        { name: "bell rail" },
      ),
    ).toBeInTheDocument();
  });

  // A cut label opens its tooltip from the link itself (WCAG 2.2, 4.1.2): no tab stop sits inside a link.
  it("AppShell_CutLabels_PutNoTabStopInsideALink", () => {
    vi.spyOn(HTMLElement.prototype, "scrollWidth", "get").mockReturnValue(400);
    vi.spyOn(HTMLElement.prototype, "clientWidth", "get").mockReturnValue(120);
    renderShell({
      settingsMenu,
      current: "members",
      crumbs: [
        { label: "Settings", href: "/northwind/settings/profile" },
        { label: "Members" },
      ],
    });

    expect(document.querySelectorAll("a [tabindex], a a")).toHaveLength(0);
    vi.restoreAllMocks();
  });

  // F59; the phone frames 2.43, 2.25 and the platform's 8.07.
  it("AppShell_Phone_StartsTheContent16pxUnderTheTopBar", () => {
    renderShell();

    expect(screen.getByRole("main")).toHaveClass("pt-4", "pb-6");
  });

  // The role editor's save bar (9.08, 9.11) sticks to the foot of the window: a panel that clips with `overflow: hidden`
  // becomes the bar's scroll box, which never scrolls, so the bar would sit at the page's end.
  it("AppShell_Panel_ClipsItsCornersWithoutHoldingStickyContent", () => {
    renderShell();

    const panel = screen.getByRole("main").parentElement;
    expect(panel).toHaveClass("tablet:overflow-clip");
    expect(panel).not.toHaveClass("tablet:overflow-hidden");
  });

  it("AppShell_Breadcrumb_EndsWithThePage", () => {
    renderShell();
    const crumbs = screen.getByRole("navigation", { name: "Breadcrumb" });

    expect(within(crumbs).getByText("Home").closest("li")).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("AppShell_Breadcrumb_SeparatesTheCrumbsWithTheDesignsMark", () => {
    renderShell();
    const crumbs = screen.getByRole("navigation", { name: "Breadcrumb" });

    const separator = within(crumbs).getByText("›");
    expect(separator).toHaveAttribute("aria-hidden", "true");
    expect(separator.querySelector("svg")).not.toBeInTheDocument();
  });

  it("AppShell_CommandK_OpensTheCommandMenuWithItsGroups", async () => {
    const onNavigate = renderShell();

    fireEvent.keyDown(window, { key: "k", metaKey: true });
    const dialog = await screen.findByRole("dialog", { name: "Command menu" });
    expect(within(dialog).getByText("Go to")).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole("option", { name: "Home" }));

    expect(onNavigate).toHaveBeenCalledWith("/northwind");
  });

  it("AppShell_CtrlK_OpensTheCommandMenu", async () => {
    renderShell();

    fireEvent.keyDown(window, { key: "k", ctrlKey: true });

    expect(
      await screen.findByRole("dialog", { name: "Command menu" }),
    ).toBeInTheDocument();
  });

  it("AppShell_CommandAction_RunsIt", async () => {
    const onSelect = vi.fn();
    renderShell({
      commands: [
        {
          id: "actions",
          label: "Actions",
          items: [{ id: "invite", label: "Invite member", onSelect }],
        },
      ],
    });

    fireEvent.keyDown(window, { key: "k", metaKey: true });
    await userEvent.click(
      await screen.findByRole("option", { name: "Invite member" }),
    );

    expect(onSelect).toHaveBeenCalledOnce();
  });

  // Only ⌘K and Ctrl K: no single key and no other chord opens or moves anything.
  it.each([
    { key: "k" },
    { key: "b", metaKey: true },
    { key: "b", ctrlKey: true },
    { key: "/" },
  ])("AppShell_OtherKey_%o_DoesNothing", (event) => {
    renderShell();

    fireEvent.keyDown(window, event);

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("AppShell_TabTitle_SetsTheDocumentTitle", () => {
    renderShell({ tabTitle: "(2) Members · Starter" });

    expect(document.title).toBe("(2) Members · Starter");
  });

  it("AppShell_Bar_ShowsAboveTheContent", () => {
    renderShell({ bar: <div role="status">You’re offline.</div> });

    expect(screen.getByRole("status")).toHaveTextContent("You’re offline.");
  });

  it("AppShell_Phone_ShowsTheMenuButtonThePageNameAndTheBell", () => {
    onAPhone();
    renderShell({
      bell: (place) => <button type="button">{`bell ${place}`}</button>,
    });

    expect(screen.getByRole("heading", { name: "Home" })).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "bell icon" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("navigation", { name: "Breadcrumb" }),
    ).not.toBeInTheDocument();
  });

  it("AppShell_PhoneMenu_OpensTheDrawerWithTheMenuAndTheVersion", async () => {
    onAPhone();
    renderShell({ version: "a1b2c3d" });

    await userEvent.click(screen.getByRole("button", { name: "Open menu" }));
    const menu = await screen.findByRole("navigation", { name: "Main menu" });

    expect(
      within(menu).getByRole("link", { name: "Home" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Version a1b2c3d")).toBeInTheDocument();
  });

  it("AppShell_PhoneMenuInSettings_ShowsTheSettingsMenuWithBack", async () => {
    onAPhone();
    renderShell({ settingsMenu, current: "members" });

    await userEvent.click(screen.getByRole("button", { name: "Open menu" }));
    const menu = await screen.findByRole("navigation", {
      name: "Settings menu",
    });

    expect(
      within(menu).getByRole("link", { name: "Back to Northwind Traders" }),
    ).toBeInTheDocument();
  });

  it("AppShell_InRtl_KeepsTheMenu", () => {
    render(
      <div dir="rtl">
        <NextIntlClientProvider locale="en" messages={en}>
          <AppShell {...base} onNavigate={vi.fn()}>
            Page
          </AppShell>
        </NextIntlClientProvider>
      </div>,
    );

    expect(
      screen.getByRole("navigation", { name: "Main menu" }),
    ).toBeInTheDocument();
  });
});
