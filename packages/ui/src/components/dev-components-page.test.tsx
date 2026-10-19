import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { DevComponentsPage } from "./dev-components-page";

// The 20 components of the design system's components/index.d.ts, and the sign-in parts of step 21b.
const names = [
  "AppShell",
  "Banner",
  "Button",
  "CodeInput",
  "Combobox",
  "Dialog",
  "EmptyState",
  "Freshness",
  "Icon",
  "Input",
  "OrgMenu",
  "ProviderButtons",
  "RowMenu",
  "SignIn",
  "Skeleton",
  "Status",
  "SubNav",
  "Table",
  "Tabs",
  "Tag",
  "Time",
  "Toast",
  "UserMenu",
];

describe("DevComponentsPage", () => {
  it("DevComponentsPage_Renders_EveryComponentOfTheDesignSystem", () => {
    render(<DevComponentsPage languages={["en", "tr", "ar"]} />);

    const sections = screen
      .getAllByRole("region")
      .map((section) => section.getAttribute("aria-labelledby"));
    expect(sections.sort()).toEqual(names.map((name) => `dev-${name}`));
  });

  // The test renders every component of the design system. On CI, beside the builds, that alone can take 5 s. It waits
  // longer; it never retries.
  it("DevComponentsPage_RailWithLabels_ShowsTheExpandedRail", {
    timeout: 15_000,
  }, async () => {
    render(<DevComponentsPage languages={["en"]} />);

    await userEvent.click(
      screen.getByRole("button", { name: "Rail with labels" }),
    );

    expect(screen.getByRole("button", { name: "Menu labels" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
  });
});
