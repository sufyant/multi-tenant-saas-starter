import { render, screen } from "@testing-library/react";
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
});
