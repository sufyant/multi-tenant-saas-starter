import {
  BuiltInRole,
  ErrorCode,
  Permission,
  type RoleDetailResponse,
} from "@repo/api-client";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
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
  maxCustomRoles,
  mehmet,
  northwind,
  permissionsOf,
  pickFromRowMenu,
  problem,
  renderInTenant,
  roleOf,
  tenantAnswers,
  viewerAnswers,
} from "../../../test/tenant";
import { RolesPage } from "./roles-page";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  push.mockReset();
});
afterAll(() => server.close());

const url = "/northwind/settings/roles";
const reviewer = customRole(
  "0198f0a2-0000-7000-8000-0000000000c3",
  "Reviewer",
  [Permission.TenantProfileRead, Permission.TenantMembersRead],
);

/** A custom role that edits and deletes roles but does not read Activity, so Billing lead is out of its reach (F51). */
const roleEditor = customRole(
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

const rows = async () =>
  within(await screen.findByRole("table", { name: "Roles" }))
    .getAllByRole("row")
    .slice(1)
    .map((row) => row.textContent);

const menuItems = async (role: string) => {
  const table = within(await screen.findByRole("table", { name: "Roles" }));
  await userEvent.click(
    await table.findByRole("button", { name: `Actions for ${role}` }),
  );
  return (await screen.findAllByRole("menuitem")).map((item) => [
    item.textContent,
    item.getAttribute("aria-disabled") === "true",
  ]);
};

/** A role that members and pending invitations use, as ListRoles counts them. */
const usedBy = (
  role: RoleDetailResponse,
  memberCount: number,
  pendingInvitationCount: number,
): RoleDetailResponse => ({ ...role, memberCount, pendingInvitationCount });

/** GetMyTenant of the Owner, with the most custom roles Northwind may have. */
const limitOf = (max: number) =>
  http.get(`*/v1/tenants/${northwind.id}`, () =>
    HttpResponse.json({
      ...northwind,
      role: roleOf.owner,
      createdAt: "2026-01-05T09:00:00Z",
      permissions: permissionsOf.owner,
      maxCustomRoles: max,
    }),
  );

const deleteAnswer = (answer: () => Response) =>
  http.delete(`*/v1/tenants/${northwind.id}/roles/${reviewer.id}`, answer);

describe("RolesPage", () => {
  it("RolesPage_Owner_ListsBuiltInRolesFirstWithCountAndType", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [billingLead, reviewer]),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await rows()).toEqual([
      expect.stringMatching(/^Owner13 permissionsNot in useBuilt in/),
      expect.stringMatching(/^Admin9 permissionsNot in useBuilt in/),
      expect.stringMatching(/^Member2 permissionsNot in useBuilt in/),
      expect.stringMatching(/^Billing lead3 permissionsNot in use$/),
      expect.stringMatching(/^Reviewer2 permissionsNot in use$/),
    ]);
    expect(screen.getByRole("button", { name: "Create role" })).toHaveAttribute(
      "href",
      "/northwind/settings/roles/new",
    );
    expect(screen.queryByText("No custom roles yet")).not.toBeInTheDocument();
  });

  it("RolesPage_OwnerOnBuiltInRole_OffersViewPermissionsOnly", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<RolesPage />, { url });

    expect(await menuItems("Admin")).toEqual([["View permissions", false]]);
  });

  it("RolesPage_OwnerOnCustomRole_OffersEditAndDelete", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]));

    renderInTenant(<RolesPage />, { url });

    expect(await menuItems("Reviewer")).toEqual([
      ["Edit role", false],
      ["Delete role", false],
    ]);
  });

  it("RolesPage_EditRole_OpensTheRolesPage", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]));

    renderInTenant(<RolesPage />, { url });
    await pickFromRowMenu("Roles", "Reviewer", "Edit role");

    expect(push).toHaveBeenCalledWith(
      `/northwind/settings/roles/${reviewer.id}`,
    );
  });

  it("RolesPage_Admin_ReadsOnlyWithoutCreate", async () => {
    server.use(...tenantAnswers(BuiltInRole.Admin, undefined, [reviewer]));

    renderInTenant(<RolesPage />, { url });

    expect(await menuItems("Reviewer")).toEqual([["View permissions", false]]);
    expect(
      screen.queryByRole("button", { name: "Create role" }),
    ).not.toBeInTheDocument();
  });

  it("RolesPage_CustomViewerWithoutEveryPermission_DisablesEditAndDeleteWithTheReason", async () => {
    server.use(
      ...viewerAnswers(roleEditor, roleEditor.permissions, mehmet, [
        roleEditor,
        billingLead,
      ]),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await menuItems("Billing lead")).toEqual([
      ["View permissions", false],
      ["Edit roleThis role has permissions your role doesn’t have.", true],
      ["Delete roleThis role has permissions your role doesn’t have.", true],
    ]);
  });

  it.each([
    [1, 0, "1 member"],
    [4, 0, "4 members"],
    [0, 1, "1 pending invitation"],
    [0, 3, "3 pending invitations"],
    [4, 1, "4 members · 1 pending invitation"],
    [1, 2, "1 member · 2 pending invitations"],
    [0, 0, "Not in use"],
  ])(
    "RolesPage_RoleUsedBy%sMembersAnd%sInvitations_SaysWhoUsesIt",
    async (members, invitations, text) => {
      server.use(
        ...tenantAnswers(BuiltInRole.Owner, undefined, [
          usedBy(reviewer, members, invitations),
        ]),
      );

      renderInTenant(<RolesPage />, { url });

      expect((await rows())[3]).toBe(`Reviewer2 permissions${text}`);
    },
  );

  it.each([
    [4, 1, "Give its 4 members and 1 pending invitation another role first."],
    [1, 0, "Give its 1 member another role first."],
    [2, 3, "Give its 2 members and 3 pending invitations another role first."],
  ])(
    "RolesPage_CustomRoleUsedBy%sMembersAnd%sInvitations_DisablesDeleteWithTheCounts",
    async (members, invitations, reason) => {
      server.use(
        ...tenantAnswers(BuiltInRole.Owner, undefined, [
          usedBy(reviewer, members, invitations),
        ]),
      );

      renderInTenant(<RolesPage />, { url });

      expect(await menuItems("Reviewer")).toEqual([
        ["Edit role", false],
        ["See its members", false],
        [`Delete role${reason}`, true],
      ]);
    },
  );

  it("RolesPage_SeeItsMembers_OpensTheMembersFilteredByTheRole", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [usedBy(reviewer, 4, 1)]),
    );

    renderInTenant(<RolesPage />, { url });
    await pickFromRowMenu("Roles", "Reviewer", "See its members");

    expect(push).toHaveBeenCalledWith(
      `/northwind/settings/members?roleId=${reviewer.id}`,
    );
  });

  it("RolesPage_CustomRoleUsedOnlyByInvitations_DisablesDeleteAndLinksItsInvitations", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [usedBy(reviewer, 0, 2)]),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await menuItems("Reviewer")).toEqual([
      ["Edit role", false],
      ["See its invitations", false],
      ["Delete roleGive its 2 pending invitations another role first.", true],
    ]);
    await userEvent.click(
      screen.getByRole("menuitem", { name: "See its invitations" }),
    );
    expect(push).toHaveBeenCalledWith(
      `/northwind/settings/members?tab=invitations&roleId=${reviewer.id}`,
    );
  });

  it("RolesPage_BuiltInRoleInUse_OffersItsMembers", async () => {
    server.use(
      http.get(`*/v1/tenants/${northwind.id}/roles`, () =>
        HttpResponse.json({
          items: [
            {
              ...roleOf.admin,
              permissions: permissionsOf.admin,
              memberCount: 2,
              pendingInvitationCount: 1,
            },
          ],
          page: 1,
          pageSize: 100,
          total: 1,
        }),
      ),
      ...tenantAnswers(BuiltInRole.Owner),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await menuItems("Admin")).toEqual([
      ["View permissions", false],
      ["See its members", false],
    ]);
  });

  it("RolesPage_CustomRoleInUseAndOutOfReach_ShowsTheEscalationReason", async () => {
    server.use(
      ...viewerAnswers(roleEditor, roleEditor.permissions, mehmet, [
        roleEditor,
        usedBy(billingLead, 1, 1),
      ]),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await menuItems("Billing lead")).toEqual([
      ["View permissions", false],
      ["See its members", false],
      ["Edit roleThis role has permissions your role doesn’t have.", true],
      ["Delete roleThis role has permissions your role doesn’t have.", true],
    ]);
  });

  it("RolesPage_UnderTheLimit_CountsTheCustomRoles", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [billingLead, reviewer]),
    );

    renderInTenant(<RolesPage />, { url });

    expect(
      await screen.findByText(`2 of ${maxCustomRoles} custom roles`),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Create role" })).toHaveAttribute(
      "href",
      "/northwind/settings/roles/new",
    );
  });

  it.each([
    [
      2,
      "Your organisation has 2 custom roles, the most it can have. Delete one to create another.",
    ],
    [
      1,
      "Your organisation has 1 custom role, the most it can have. Delete one to create another.",
    ],
  ])(
    "RolesPage_LimitOf%sWithTwoCustomRoles_DisablesCreateWithTheReason",
    async (max, reason) => {
      server.use(
        limitOf(max),
        ...tenantAnswers(BuiltInRole.Owner, undefined, [billingLead, reviewer]),
      );

      renderInTenant(<RolesPage />, { url });

      const create = await screen.findByRole("button", {
        name: "Create role",
      });
      await waitFor(() => expect(create).toBeDisabled());
      expect(create).not.toHaveAttribute("href");
      expect(create).toHaveAccessibleDescription(reason);
      expect(screen.queryByText(/custom roles$/)).not.toBeInTheDocument();
    },
  );

  it("RolesPage_WithoutCreate_ShowsNoCounter", async () => {
    server.use(...tenantAnswers(BuiltInRole.Admin, undefined, [reviewer]));

    renderInTenant(<RolesPage />, { url });

    expect(await rows()).toHaveLength(4);
    expect(screen.queryByText(/custom role/)).not.toBeInTheDocument();
  });

  it("RolesPage_OnlyBuiltInRoles_ShowsTheEmptyStateUnderTheTable", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    renderInTenant(<RolesPage />, { url });

    expect(await screen.findByText("No custom roles yet")).toBeInTheDocument();
    expect(await rows()).toHaveLength(3);
    expect(screen.getAllByRole("button", { name: "Create role" })).toHaveLength(
      2,
    );
  });

  it("RolesPage_WithoutRolesRead_ShowsPermissionDenied", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member));

    renderInTenant(<RolesPage />, { url });

    expect(
      await screen.findByRole("heading", { name: "You can’t open Roles" }),
    ).toBeInTheDocument();
  });
});

describe("DeleteRoleDialog", () => {
  const openDelete = async () => {
    await pickFromRowMenu("Roles", "Reviewer", "Delete role");
    return screen.findByRole("alertdialog", { name: "Delete Reviewer?" });
  };

  it("DeleteRole_Success_ClosesToastsAndReadsTheRolesAgain", async () => {
    let reads = 0;
    const page = (items: RoleDetailResponse[]) => () => {
      reads += 1;
      return HttpResponse.json({ items, page: 1, pageSize: 100, total: 1 });
    };
    server.use(
      http.get(`*/v1/tenants/${northwind.id}/roles`, page([reviewer]), {
        once: true,
      }),
      http.get(`*/v1/tenants/${northwind.id}/roles`, page([])),
      ...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]),
      deleteAnswer(() => new HttpResponse(null, { status: 204 })),
    );

    renderInTenant(<RolesPage />, { url });
    const dialog = await openDelete();
    expect(dialog).toHaveTextContent(
      "People can no longer get this role. Activity keeps its name.",
    );
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Delete role" }),
    );

    expect(await screen.findByText("Role deleted")).toBeInTheDocument();
    expect(screen.getByText("Activity keeps its name.")).toBeInTheDocument();
    await waitFor(() => expect(reads).toBe(2));
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
  });

  it.each([
    [
      409,
      ErrorCode.RoleInUse,
      "Reviewer is in use now.",
      "Someone gave it to a member or an invitation a moment ago. The list has been refreshed.",
    ],
    [
      404,
      ErrorCode.RoleNotFound,
      "This role no longer exists.",
      "The list has been refreshed.",
    ],
    [
      403,
      ErrorCode.RoleEscalation,
      "This role has permissions your role doesn’t have.",
      "Ask an Owner to delete it.",
    ],
    [
      409,
      ErrorCode.RoleBuiltIn,
      "Built-in roles can’t be edited or deleted.",
      "Create a custom role to give a different set of permissions.",
    ],
  ])(
    "DeleteRole_Refused%s_%s_ShowsTheTextAndReadsTheRolesAgain",
    async (status, code, title, text) => {
      let reads = 0;
      server.use(
        http.get(`*/v1/tenants/${northwind.id}/roles`, () => {
          reads += 1;
          return HttpResponse.json({
            items: [reviewer],
            page: 1,
            pageSize: 100,
            total: 1,
          });
        }),
        ...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]),
        deleteAnswer(() => problem(status, code)),
      );

      renderInTenant(<RolesPage />, { url });
      const dialog = await openDelete();
      await userEvent.click(
        within(dialog).getByRole("button", { name: "Delete role" }),
      );

      const banner = await within(dialog).findByRole("status");
      expect(banner).toHaveTextContent(title);
      expect(banner).toHaveTextContent(text);
      await waitFor(() => expect(reads).toBe(2));
    },
  );

  it("DeleteRole_RefusedNotPermitted_ClosesWithAToast", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]),
      deleteAnswer(() => problem(403, ErrorCode.RoleNotPermitted)),
    );

    renderInTenant(<RolesPage />, { url });
    const dialog = await openDelete();
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Delete role" }),
    );

    expect(
      await screen.findByText("Your role can’t change roles."),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument(),
    );
  });
});
