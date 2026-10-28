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
  customRole,
  free,
  renderInTenant,
  roleOf,
  tenantAnswers,
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
  it("RolePage_BuiltInOwner_ShowsTheBannerEveryPermissionAndNoForm", async () => {
    open(roleOf.owner.id);

    expect(
      await screen.findByRole("heading", { name: "Owner" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Built-in roles can’t be edited or deleted."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
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
        "See Activity",
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
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
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
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    expect(
      screen.getByText(
        "Your organisation’s plan doesn’t include custom roles.",
      ),
    ).toBeVisible();
    expect(
      screen.getByRole("checkbox", { name: "See members" }),
    ).toBeDisabled();
  });

  it("RolePage_UnknownId_SaysTheRoleIsGone", async () => {
    open("0198f0a2-0000-7000-8000-0000000000ff");

    expect(
      await screen.findByText("This role no longer exists."),
    ).toBeInTheDocument();
  });
});
