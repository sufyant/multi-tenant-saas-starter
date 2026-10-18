import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { DevComponentsPage } from "./dev-components-page";

// The 20 components of the design system's components/index.d.ts.
const names = [
  "AppShell",
  "Banner",
  "Button",
  "Combobox",
  "Dialog",
  "EmptyState",
  "Freshness",
  "Icon",
  "Input",
  "OrgMenu",
  "RowMenu",
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
