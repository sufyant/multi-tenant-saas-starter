import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { DevComponentGroup, devComponentGroups } from "./dev-components-page";

// The 20 components of the design system's components/index.d.ts, the sign-in parts of step 21b, the preferences form of step 22e,
// the activity log of step 22g and the shared parts and controls of step 22k.
const names = [
  "ActivityLog",
  "AppShell",
  "Avatar",
  "Banner",
  "Button",
  "Checkbox",
  "CodeInput",
  "Combobox",
  "Dialog",
  "EmptyState",
  "Freshness",
  "Icon",
  "Input",
  "ListError",
  "OrgMenu",
  "PreferencesForm",
  "ProviderButtons",
  "RadioGroup",
  "RowMenu",
  "Search",
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

  it("DevComponentGroup_Checkbox_ShowsEachState", () => {
    render(<DevComponentGroup name="Checkbox" />);

    expect(screen.getByRole("checkbox", { name: "See members" })).toBeChecked();
    expect(
      screen.getByRole("checkbox", { name: "See the organisation’s details" }),
    ).toBeDisabled();
    expect(
      screen.getByRole("checkbox", { name: "Manage roles" }),
    ).toHaveAccessibleDescription("Your role doesn’t have this permission.");
  });

  it("DevComponentGroup_RadioGroup_ShowsADisabledRoleWithItsReason", () => {
    render(<DevComponentGroup name="RadioGroup" />);

    expect(
      screen.getByRole("radio", { name: "Owner" }),
    ).toHaveAccessibleDescription(
      expect.stringContaining("Only an Owner can give the Owner role."),
    );
    expect(screen.getByRole("radio", { name: "Member" })).toBeChecked();
  });
});
