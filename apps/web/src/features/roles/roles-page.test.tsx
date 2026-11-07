import {
  BuiltInRole,
  ErrorCode,
  Permission,
  type RoleDetailResponse,
} from "@repo/api-client";
import { brand } from "@repo/brand";
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
  free,
  mehmet,
  northwind,
  permissionsOf,
  pickFromRowMenu,
  planWith,
  problem,
  renderInTenant,
  roleOf,
  supportLead,
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

const deleteAnswer = (answer: () => Response) =>
  http.delete(`*/v1/tenants/${northwind.id}/roles/${reviewer.id}`, answer);

describe("RolesPage", () => {
  it("RolesPage_Owner_ListsBuiltInRolesFirstWithCountAndType", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [billingLead, reviewer]),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await rows()).toEqual([
      expect.stringMatching(/^OwnerBuilt inNot in use13 permissions/),
      expect.stringMatching(/^AdminBuilt inNot in use9 permissions/),
      expect.stringMatching(/^MemberBuilt inNot in use2 permissions/),
      expect.stringMatching(/^Billing leadCustomNot in use3 permissions/),
      expect.stringMatching(/^ReviewerCustomNot in use2 permissions/),
    ]);
    expect(screen.getByRole("button", { name: "New role" })).toHaveAttribute(
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
      screen.queryByRole("button", { name: "New role" }),
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

      expect((await rows())[3]).toBe(`ReviewerCustom${text}2 permissions`);
    },
  );

  // A role in use keeps "Delete role", which opens a dialog that says who uses it and how to go on (9.13; the rule of D4).
  it.each([
    [
      4,
      1,
      "4 members and 1 pending invitation have this role. Give them another role first.",
    ],
    [1, 0, "1 member has this role. Give its member another role first."],
    [3, 0, "3 members have this role. Give its members another role first."],
  ])(
    "RolesPage_DeleteCustomRoleUsedBy%sMembersAnd%sInvitations_SaysWhoUsesItAndOffersItsMembers",
    async (members, invitations, text) => {
      server.use(
        ...tenantAnswers(BuiltInRole.Owner, undefined, [
          usedBy(reviewer, members, invitations),
        ]),
      );

      renderInTenant(<RolesPage />, { url });
      expect(await menuItems("Reviewer")).toEqual([
        ["Edit role", false],
        ["See its members", false],
        ["Delete role", false],
      ]);
      await userEvent.click(
        screen.getByRole("menuitem", { name: "Delete role" }),
      );

      const dialog = await screen.findByRole("alertdialog", {
        name: "Reviewer is in use",
      });
      expect(dialog).toHaveTextContent(text);
      await userEvent.click(
        within(dialog).getByRole("button", { name: "See its members" }),
      );
      expect(push).toHaveBeenCalledWith(
        `/northwind/settings/members?roleId=${reviewer.id}`,
      );
    },
  );

  it("RolesPage_DeleteRoleInUse_CallsNoDelete", async () => {
    const deleted = vi.fn();
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [usedBy(reviewer, 1, 0)]),
      deleteAnswer(() => {
        deleted();
        return new HttpResponse(null, { status: 204 });
      }),
    );

    renderInTenant(<RolesPage />, { url });
    await pickFromRowMenu("Roles", "Reviewer", "Delete role");
    const dialog = await screen.findByRole("alertdialog", {
      name: "Reviewer is in use",
    });
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Close" }),
    );

    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
    expect(deleted).not.toHaveBeenCalled();
  });

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

  it("RolesPage_CustomRoleUsedOnlyByInvitations_DeleteSaysSoAndLinksItsInvitations", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [usedBy(reviewer, 0, 2)]),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await menuItems("Reviewer")).toEqual([
      ["Edit role", false],
      ["See its invitations", false],
      ["Delete role", false],
    ]);
    await userEvent.click(
      screen.getByRole("menuitem", { name: "Delete role" }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Reviewer is in use",
    });
    expect(dialog).toHaveTextContent(
      "2 pending invitations have this role. Give its invitations another role first.",
    );
    await userEvent.click(
      within(dialog).getByRole("button", { name: "See its invitations" }),
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

  it("RolesPage_ViewerWithoutMembersRead_OffersNoMembersLink", async () => {
    const viewer = customRole(
      "0198f0a2-0000-7000-8000-0000000000c5",
      "Role keeper",
      [
        Permission.TenantProfileRead,
        Permission.TenantRolesRead,
        Permission.TenantRolesUpdate,
        Permission.TenantRolesDelete,
      ],
    );
    const profileOnly = customRole(
      "0198f0a2-0000-7000-8000-0000000000c6",
      "Profile only",
      [Permission.TenantProfileRead],
    );
    server.use(
      ...viewerAnswers(viewer, viewer.permissions, mehmet, [
        viewer,
        usedBy(profileOnly, 4, 1),
      ]),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await menuItems("Profile only")).toEqual([
      ["Edit role", false],
      ["Delete role", false],
    ]);
    await userEvent.click(
      screen.getByRole("menuitem", { name: "Delete role" }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Profile only is in use",
    });
    // Without a list to open, the dialog only informs.
    expect(within(dialog).getAllByRole("button")).toHaveLength(1);
    expect(within(dialog).getByRole("button", { name: "Close" })).toBeVisible();
  });

  it("RolesPage_ViewerWithoutInvitationsRead_OffersNoInvitationsLink", async () => {
    server.use(
      ...viewerAnswers(roleEditor, roleEditor.permissions, mehmet, [
        roleEditor,
        usedBy(reviewer, 0, 2),
      ]),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await menuItems("Reviewer")).toEqual([
      ["Edit role", false],
      ["Delete role", false],
    ]);
  });

  it("RolesPage_UnderTheLimit_CountsTheCustomRolesOfTheUsage", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [billingLead, reviewer],
        planWith({ "roles.custom.max": 20 }, { "roles.custom.max": 15 }),
      ),
    );

    renderInTenant(<RolesPage />, { url });

    expect(
      await screen.findByText("15 of 20 custom roles"),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New role" })).toHaveAttribute(
      "href",
      "/northwind/settings/roles/new",
    );
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("RolesPage_Unlimited_ShowsNoCountAndCreates", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [billingLead, reviewer],
        planWith({ "roles.custom.max": null }, { "roles.custom.max": 2 }),
      ),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await rows()).toHaveLength(5);
    expect(screen.queryByText(/custom roles?$/)).not.toBeInTheDocument();
    expect(screen.queryByText(/null/)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New role" })).toHaveAttribute(
      "href",
      "/northwind/settings/roles/new",
    );
  });

  it.each([
    [
      2,
      "Your organisation’s plan allows 2 custom roles. Delete one to create another.",
    ],
    [
      1,
      "Your organisation’s plan allows 1 custom role. Delete one to create another.",
    ],
  ])(
    "RolesPage_AtALimitOf%s_DisablesCreateWithTheReason",
    async (max, reason) => {
      server.use(
        ...tenantAnswers(
          BuiltInRole.Owner,
          undefined,
          [billingLead, reviewer],
          planWith({ "roles.custom.max": max }, { "roles.custom.max": max }),
        ),
      );

      renderInTenant(<RolesPage />, { url });

      const create = await screen.findByRole("button", {
        name: "New role",
      });
      await waitFor(() => expect(create).toBeDisabled());
      expect(create).not.toHaveAttribute("href");
      expect(create).toHaveAccessibleDescription(reason);
      expect(screen.getByText(reason)).toBeVisible();
      expect(screen.queryByRole("status")).not.toBeInTheDocument();
    },
  );

  it("RolesPage_OverTheLimit_DisablesCreateAndShowsTheBanner", async () => {
    // A limit lowered after the tenant made its roles: 3 custom roles against a limit of 2.
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [billingLead, reviewer, supportLead],
        planWith({ "roles.custom.max": 2 }, { "roles.custom.max": 3 }),
      ),
    );

    renderInTenant(<RolesPage />, { url });

    const banner = await screen.findByRole("status");
    expect(banner).toHaveTextContent(
      "You have 3 custom roles; your plan allows 2. Delete roles to create new ones. Every role and its members keep working.",
    );
    expect(
      within(banner).getByRole("link", { name: "Contact us" }),
    ).toHaveAttribute("href", brand.supportUrl);
    expect(screen.getByText("3 of 2 custom roles")).toBeVisible();
    const create = screen.getByRole("button", { name: "New role" });
    expect(create).toBeDisabled();
    expect(create).toHaveAccessibleDescription(
      "You have 3 custom roles; your plan allows 2. Delete roles to create new ones. Every role and its members keep working.",
    );
    // Over the limit, nothing is locked: a role not in use still deletes.
    expect(await menuItems("Reviewer")).toEqual([
      ["Edit role", false],
      ["Delete role", false],
    ]);
  });

  it("RolesPage_OverTheLimitWithoutCreate_ShowsNoBannerAndNoCount", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Admin,
        undefined,
        [billingLead, reviewer, supportLead],
        planWith({ "roles.custom.max": 2 }, { "roles.custom.max": 3 }),
      ),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await rows()).toHaveLength(6);
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    expect(screen.queryByText(/custom roles?$/)).not.toBeInTheDocument();
  });

  it("RolesPage_CustomRolesOffOnFree_ShowsTheReasonInPlaceOfZeroOfZero", async () => {
    // Free: roles.custom off and roles.custom.max 0. The board shows the reason, never "0 of 0 custom roles".
    server.use(...tenantAnswers(BuiltInRole.Owner, undefined, [], free));

    renderInTenant(<RolesPage />, { url });

    const reason = "Your organisation’s plan doesn’t include custom roles.";
    const [create] = await screen.findAllByRole("button", {
      name: "New role",
    });
    await waitFor(() => expect(create).toBeDisabled());
    expect(create).toHaveAccessibleDescription(reason);
    expect(screen.queryByText(/0 of 0/)).not.toBeInTheDocument();
  });

  it("RolesPage_CustomRolesOff_ListsThemClosesEditAndKeepsDelete", async () => {
    // A tenant moved to a plan without custom roles: 3 custom roles, over a limit of 0. The feature's reason shows, no banner.
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [usedBy(billingLead, 1, 0), reviewer, supportLead],
        planWith(
          { "roles.custom": false, "roles.custom.max": 0 },
          { "roles.custom.max": 3 },
          "Free",
        ),
      ),
    );

    renderInTenant(<RolesPage />, { url });

    const reason = "Your organisation’s plan doesn’t include custom roles.";
    expect(await rows()).toEqual([
      expect.stringMatching(/^Owner/),
      expect.stringMatching(/^Admin/),
      expect.stringMatching(/^Member/),
      expect.stringMatching(/^Billing lead/),
      expect.stringMatching(/^Reviewer/),
      expect.stringMatching(/^Support lead/),
    ]);
    const create = screen.getByRole("button", { name: "New role" });
    expect(create).toBeDisabled();
    expect(create).toHaveAccessibleDescription(reason);
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    expect(screen.queryByText(/custom roles$/)).not.toBeInTheDocument();
    expect(await menuItems("Reviewer")).toEqual([
      ["View permissions", false],
      [`Edit role${reason}`, true],
      ["Delete role", false],
    ]);
  });

  it("RolesPage_CustomRolesOffAndRoleInUse_KeepsDeleteEnabled", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [usedBy(reviewer, 1, 1)],
        planWith(
          { "roles.custom": false, "roles.custom.max": 0 },
          { "roles.custom.max": 1 },
        ),
      ),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await menuItems("Reviewer")).toEqual([
      ["View permissions", false],
      ["See its members", false],
      ["Edit roleYour organisation’s plan doesn’t include custom roles.", true],
      ["Delete role", false],
    ]);
  });

  it("RolesPage_ArabicOverTheLimit_ShowsTheCountAndTheBanner", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [billingLead, reviewer, supportLead],
        planWith({ "roles.custom.max": 2 }, { "roles.custom.max": 3 }),
      ),
    );

    renderInTenant(<RolesPage />, { url, locale: "ar" });

    expect(await screen.findByRole("status")).toHaveTextContent(
      "لديك 3 من الأدوار المخصصة؛ وتسمح خطتك بـ2.",
    );
    expect(screen.getByText("3 من 2 من الأدوار المخصصة")).toBeVisible();
    expect(screen.getByRole("link", { name: "تواصل معنا" })).toBeVisible();
  });

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
    // The header's "New role" is the one action (9.04).
    expect(screen.getAllByRole("button", { name: "New role" })).toHaveLength(1);
  });

  // No Freshness under the title (F59): the frame's description sentence stands there (9.01).
  it("RolesPage_Header_ShowsTheDescriptionAndNoFreshness", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]));

    renderInTenant(<RolesPage />, { url });

    expect(await rows()).toHaveLength(4);
    expect(
      screen.getByText(
        "Owner, Admin and Member are built in. Create a role when a person needs a different set of permissions.",
      ),
    ).toBeVisible();
    expect(screen.queryByText(/Updated/)).not.toBeInTheDocument();
  });

  // The list grows with Load more (F9): the next page joins the rows, and a live region says how many came.
  it("RolesPage_LoadMore_AddsTheNextPageAndSaysHowManyCame", async () => {
    const pages: Record<string, RoleDetailResponse[]> = {
      "1": [
        {
          ...roleOf.owner,
          permissions: permissionsOf.owner,
          memberCount: 1,
          pendingInvitationCount: 0,
        },
        {
          ...roleOf.admin,
          permissions: permissionsOf.admin,
          memberCount: 0,
          pendingInvitationCount: 0,
        },
      ],
      "2": [reviewer],
    };
    server.use(
      http.get(`*/v1/tenants/${northwind.id}/roles`, ({ request }) => {
        const page = new URL(request.url).searchParams.get("page") ?? "1";
        return HttpResponse.json({
          items: pages[page],
          page: Number(page),
          pageSize: 2,
          total: 3,
        });
      }),
      ...tenantAnswers(BuiltInRole.Owner),
    );

    renderInTenant(<RolesPage />, { url });
    expect(await rows()).toHaveLength(2);
    await userEvent.click(screen.getByRole("button", { name: "Load more" }));

    await waitFor(async () => expect(await rows()).toHaveLength(3));
    expect(screen.getByText("1 more role loaded.")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Load more" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Next" }),
    ).not.toBeInTheDocument();
  });

  it("RolesPage_WithoutRolesRead_ShowsPermissionDenied", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member));

    renderInTenant(<RolesPage />, { url });

    expect(
      await screen.findByRole("heading", { name: "You can’t open this page" }),
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
      "People can no longer get this role. The Activity log keeps its name.",
    );
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Delete role" }),
    );

    expect(await screen.findByText("Role deleted")).toBeInTheDocument();
    expect(
      screen.getByText("The Activity log keeps its name."),
    ).toBeInTheDocument();
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
