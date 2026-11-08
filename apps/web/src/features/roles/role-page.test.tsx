import { BuiltInRole, Permission } from "@repo/api-client";
import { screen } from "@testing-library/react";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import {
  billingLead,
  customRole,
  free,
  mehmet,
  renderInTenant,
  roleOf,
  tenantAnswers,
  viewerAnswers,
} from "../../../test/tenant";
import { RolePage } from "./role-page";

vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn() }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

// A box is the Checkbox of packages/ui, a button: its state is aria-checked, not an input's `checked`.
const states = () =>
  screen
    .getAllByRole<HTMLButtonElement>("checkbox")
    .map((box) => [
      box.labels?.[0]?.textContent,
      box.getAttribute("aria-checked") === "true",
      box.disabled,
    ]);

const open = (roleId: string, role: BuiltInRole = BuiltInRole.Owner) => {
  server.use(
    ...tenantAnswers(role, undefined, [
      customRole("0198f0a2-0000-7000-8000-0000000000c3", "Reviewer", [
        Permission.TenantProfileRead,
        Permission.TenantMembersRead,
      ]),
    ]),
  );
  renderInTenant(<RolePage roleId={roleId} />, {
    url: `/northwind/settings/roles/${roleId}`,
  });
};

describe("RolePage", () => {
  // A role that opens read-only shows its name in a disabled field, as the editor's card does (9.10, 9.20).
  it("RolePage_BuiltInOwner_ShowsTheBannerEveryPermissionAndADisabledName", async () => {
    open(roleOf.owner.id);

    expect(
      await screen.findByRole("heading", { name: "Owner" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Built-in roles can’t be edited or deleted."),
    ).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Name" })).toBeDisabled();
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("Owner");
    expect(screen.queryByRole("button", { name: "Save changes" })).toBeNull();
    expect(states()).toEqual(
      [
        "See the organisation’s details",
        "See members",
        "Change members’ roles",
        "Remove members",
        "Manage Owners",
        "See invitations",
        "Invite people",
        "Revoke and cancel invitations",
        "See roles",
        "Create roles",
        "Edit roles",
        "Delete roles",
        "See the activity log",
      ].map((label) => [label, true, true]),
    );
  });

  it("RolePage_BuiltInMember_ShowsEachPermissionsStateWithoutManageOwners", async () => {
    open(roleOf.member.id);

    await screen.findByRole("heading", { name: "Member" });
    const checked = states()
      .filter(([, isChecked]) => isChecked)
      .map(([label]) => label);
    expect(checked).toEqual(["See the organisation’s details", "See members"]);
    expect(states()).toHaveLength(12);
    expect(
      screen.queryByRole("checkbox", { name: "Manage Owners" }),
    ).not.toBeInTheDocument();
  });

  it("RolePage_CustomRoleWithoutUpdate_ReadsWithoutAForm", async () => {
    open("0198f0a2-0000-7000-8000-0000000000c3", BuiltInRole.Admin);

    expect(
      await screen.findByRole("heading", { name: "Reviewer" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Name" })).toBeDisabled();
    expect(
      screen.queryByText("Built-in roles can’t be edited or deleted."),
    ).not.toBeInTheDocument();
  });

  it("RolePage_CustomRoleWithCustomRolesOff_ReadsWithTheReason", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [
          customRole("0198f0a2-0000-7000-8000-0000000000c3", "Reviewer", [
            Permission.TenantProfileRead,
            Permission.TenantMembersRead,
          ]),
        ],
        free,
      ),
    );
    renderInTenant(<RolePage roleId="0198f0a2-0000-7000-8000-0000000000c3" />, {
      url: "/northwind/settings/roles/0198f0a2-0000-7000-8000-0000000000c3",
    });

    expect(
      await screen.findByRole("heading", { name: "Reviewer" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Name" })).toBeDisabled();
    expect(
      screen.getByText(
        "Your organisation’s plan doesn’t include custom roles.",
      ),
    ).toBeVisible();
    expect(
      screen.getByRole("checkbox", { name: "See members" }),
    ).toBeDisabled();
  });

  // Under the title: the type, the permissions and who uses the role, and a way back to the list (9.10).
  it("RolePage_BuiltInRole_ShowsBackToRolesAndItsSummary", async () => {
    open(roleOf.member.id);

    await screen.findByRole("heading", { name: "Member" });
    expect(
      screen.getByText("Built in · 2 permissions · not in use"),
    ).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Back to Roles" }),
    ).toHaveAttribute("href", "/northwind/settings/roles");
  });

  // A role out of the viewer's reach reads with the reason and who can change it (9.20, F51).
  it("RolePage_RoleOutOfReach_SaysWhyAndAsksAnOwner", async () => {
    const keeper = customRole(
      "0198f0a2-0000-7000-8000-0000000000c4",
      "Role editor",
      [
        Permission.TenantProfileRead,
        Permission.TenantMembersRead,
        Permission.TenantRolesRead,
        Permission.TenantRolesUpdate,
        Permission.TenantRolesDelete,
      ],
    );
    server.use(
      ...viewerAnswers(keeper, keeper.permissions, mehmet, [
        keeper,
        { ...billingLead, memberCount: 1, pendingInvitationCount: 0 },
      ]),
    );
    renderInTenant(<RolePage roleId={billingLead.id} />, {
      url: `/northwind/settings/roles/${billingLead.id}`,
    });

    await screen.findByRole("heading", { name: "Billing lead" });
    expect(screen.getByText("Custom · 3 permissions · 1 member")).toBeVisible();
    expect(
      screen.getByText("This role has permissions your role doesn’t have."),
    ).toBeVisible();
    expect(
      screen.getByText(
        "You can see it, but only someone who holds all of them can change or delete it. Ask an Owner.",
      ),
    ).toBeVisible();
    expect(screen.getByRole("textbox", { name: "Name" })).toBeDisabled();
  });

  it("RolePage_UnknownId_SaysTheRoleIsGone", async () => {
    open("0198f0a2-0000-7000-8000-0000000000ff");

    expect(
      await screen.findByText("This role no longer exists."),
    ).toBeInTheDocument();
  });
});
