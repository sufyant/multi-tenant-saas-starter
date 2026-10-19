import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { DevComponentGroup, devComponentGroups } from "./dev-components-page";

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
  it("DevComponentsPage_Groups_AreEveryComponentOfTheDesignSystem", () => {
    expect([...devComponentGroups].sort()).toEqual(names);
  });

  // One group at a time: the whole page at once takes over 5 s on CI, beside the builds.
  it.each(names)("DevComponentGroup_%s_RendersItsPreviews", (name) => {
    render(<DevComponentGroup name={name} />);

    expect(screen.getByRole("region", { name })).toBeInTheDocument();
  });

  it("DevComponentGroup_AppShellRailWithLabels_ShowsTheExpandedRail", async () => {
    render(<DevComponentGroup name="AppShell" />);

    await userEvent.click(
      screen.getByRole("button", { name: "Rail with labels" }),
    );

    expect(screen.getByRole("button", { name: "Menu labels" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
  });
});
