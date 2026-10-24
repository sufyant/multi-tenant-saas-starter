import { ErrorCode, Permission } from "@repo/api-client";
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
import { BuiltInRole } from "@/shared/roles";
import {
  billingLead,
  customRole,
  mehmet,
  northwind,
  pickFromRowMenu,
  problem,
  renderInTenant,
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

const deleteAnswer = (answer: () => Response) =>
  http.delete(`*/v1/tenants/${northwind.id}/roles/${reviewer.id}`, answer);

describe("RolesPage", () => {
  it("RolesPage_Owner_ListsBuiltInRolesFirstWithCountAndType", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [billingLead, reviewer]),
    );

    renderInTenant(<RolesPage />, { url });

    expect(await rows()).toEqual([
      expect.stringMatching(/^Owner13 permissionsBuilt in/),
      expect.stringMatching(/^Admin9 permissionsBuilt in/),
      expect.stringMatching(/^Member2 permissionsBuilt in/),
      expect.stringMatching(/^Billing lead3 permissions$/),
      expect.stringMatching(/^Reviewer2 permissions$/),
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
    server.use(
      http.get(`*/v1/tenants/${northwind.id}/roles`, () => {
        reads += 1;
        const items = reads === 1 ? [reviewer] : [];
        return HttpResponse.json({ items, page: 1, pageSize: 100, total: 1 });
      }),
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
