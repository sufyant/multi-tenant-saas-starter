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
import {
  ayse,
  billingLead,
  customRole,
  john,
  mehmet,
  membersAnswer,
  northwind,
  people,
  permissionsOf,
  pickFromRowMenu,
  problem,
  renderInTenant,
  roleOf,
  supportLead,
  tenantAnswers,
  viewerAnswers,
} from "../../../test/tenant";

/** GetMyTenant for an Owner, counted: a second read means the screen refreshed the caller's permissions. */
const countedTenant = (reads: string[]) =>
  http.get(`*/v1/tenants/${northwind.id}`, () => {
    reads.push("read");
    return HttpResponse.json({
      ...northwind,
      createdAt: "2026-01-05T09:00:00Z",
      permissions: permissionsOf.owner,
    });
  });

import { BuiltInRole } from "@repo/api-client";
import { MembersPage } from "./members-page";

vi.mock("@clerk/nextjs", () => ({ useClerk: () => ({ signOut: vi.fn() }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const changeAnswer = (answer: () => Response, bodies: unknown[] = []) =>
  http.patch(
    `*/v1/tenants/${northwind.id}/members/${john.userId}`,
    async ({ request }) => {
      bodies.push(await request.json());
      return answer();
    },
  );

const openChangeRole = async () => {
  await pickFromRowMenu("Members", "John Byrne", "Change role");
  return screen.findByRole("alertdialog", { name: "Change John Byrne’s role" });
};

describe("ChangeRoleDialog", () => {
  it("ChangeRole_Success_ClosesToastsAndRefreshesTheList", async () => {
    const requests: URLSearchParams[] = [];
    const bodies: unknown[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(people, requests),
      changeAnswer(
        () =>
          HttpResponse.json({
            ...john,
            role: roleOf.admin,
          }),
        bodies,
      ),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openChangeRole();
    await userEvent.click(within(dialog).getByRole("radio", { name: "Admin" }));
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Change role" }),
    );

    expect(await screen.findByText("Role changed")).toBeInTheDocument();
    expect(screen.getByText("John Byrne is now an Admin.")).toBeInTheDocument();
    expect(bodies).toEqual([{ roleId: roleOf.admin.id }]);
    expect(requests).toHaveLength(2);
    await waitFor(() =>
      expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument(),
    );
  });

  it("ChangeRole_Owner_OffersEveryRoleWithItsDescription", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);
    const dialog = await openChangeRole();

    expect(within(dialog).getAllByRole("radio")).toHaveLength(3);
    expect(dialog).toHaveTextContent(
      "Everything an Admin can do, and can give or take the Owner role.",
    );
    expect(within(dialog).getByRole("radio", { name: "Member" })).toBeChecked();
  });

  it("ChangeRole_Admin_ListsTheOwnerDisabledWithItsReason", async () => {
    server.use(...tenantAnswers(BuiltInRole.Admin), membersAnswer());

    renderInTenant(<MembersPage />);
    const dialog = await openChangeRole();

    const owner = within(dialog).getByRole("radio", { name: "Owner" });
    expect(owner).toBeDisabled();
    expect(owner).toHaveAccessibleDescription(
      expect.stringContaining("Only an Owner can give the Owner role."),
    );
    expect(within(dialog).getByRole("radio", { name: "Admin" })).toBeEnabled();
  });

  it("ChangeRole_CustomRoles_ListedAfterTheBuiltInRolesWithTheirPermissionCount", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, permissionsOf.owner, [billingLead]),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openChangeRole();

    expect(
      within(dialog)
        .getAllByRole("radio")
        .map((radio) => radio.getAttribute("value")),
    ).toEqual([
      roleOf.owner.id,
      roleOf.admin.id,
      roleOf.member.id,
      billingLead.id,
    ]);
    expect(
      within(dialog).getByRole("radio", { name: "Billing lead" }),
    ).toHaveAccessibleDescription("3 permissions");
  });

  it("ChangeRole_RoleTheViewerDoesNotFullyHold_DisabledWithTheEscalationText", async () => {
    const roleEditor = customRole(
      "0198f0a2-0000-7000-8000-0000000000c3",
      "Role editor",
      [
        Permission.TenantProfileRead,
        Permission.TenantRolesRead,
        Permission.TenantRolesUpdate,
      ],
    );
    server.use(
      ...tenantAnswers(BuiltInRole.Admin, permissionsOf.admin, [roleEditor]),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openChangeRole();

    const radio = within(dialog).getByRole("radio", { name: "Role editor" });
    expect(radio).toBeDisabled();
    expect(radio).toHaveAccessibleDescription(
      expect.stringContaining(
        "This role has permissions your role doesn’t have.",
      ),
    );
  });

  it("ChangeRole_CustomViewer_GivesOnlyTheRolesItFullyHolds", async () => {
    server.use(
      ...viewerAnswers(
        supportLead,
        supportLead.permissions,
        { ...mehmet, role: supportLead },
        [supportLead, billingLead],
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openChangeRole();

    expect(
      within(dialog)
        .getAllByRole("radio")
        .filter((radio) => !(radio as HTMLInputElement).disabled)
        .map((radio) => radio.getAttribute("value")),
    ).toEqual([roleOf.member.id, supportLead.id]);
  });

  it("ChangeRole_RefusedRoleNotFound_ReadsTheRolesAgain", async () => {
    const reads: string[] = [];
    server.use(
      http.get(`*/v1/tenants/${northwind.id}/roles`, () => {
        reads.push("read");
        const items = Object.values(BuiltInRole).map((name) => ({
          ...roleOf[name],
          permissions: permissionsOf[name],
        }));
        return HttpResponse.json({ items, page: 1, pageSize: 100, total: 3 });
      }),
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      changeAnswer(() => problem(404, ErrorCode.RoleNotFound)),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openChangeRole();
    const before = reads.length;
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Change role" }),
    );

    await waitFor(() => expect(reads.length).toBeGreaterThan(before));
  });

  it.each([
    [
      409,
      ErrorCode.MemberLastOwner,
      "Northwind Traders needs at least one Owner.",
      "Make someone else an Owner first, then change this role.",
    ],
    [
      403,
      ErrorCode.MemberOwnerRequiresOwner,
      "Only an Owner can give or take the Owner role.",
      "Ask an Owner of Northwind Traders to make this change.",
    ],
    [
      404,
      ErrorCode.MemberNotFound,
      "John Byrne is no longer a member.",
      "Someone removed them while this dialog was open. The list has been refreshed.",
    ],
    [
      403,
      ErrorCode.RoleEscalation,
      "This role has permissions your role doesn’t have.",
      "Choose another role, or ask an Owner.",
    ],
    [
      404,
      ErrorCode.RoleNotFound,
      "This role no longer exists.",
      "The list has been refreshed.",
    ],
  ])(
    "ChangeRole_Refused%s_%s_ShowsTheBoardsTextInTheDialog",
    async (status, code, title, text) => {
      server.use(
        ...tenantAnswers(BuiltInRole.Owner),
        membersAnswer(),
        changeAnswer(() => problem(status, code)),
      );

      renderInTenant(<MembersPage />);
      const dialog = await openChangeRole();
      await userEvent.click(
        within(dialog).getByRole("button", { name: "Change role" }),
      );

      const banner = await within(dialog).findByRole("status");
      expect(banner).toHaveTextContent(title);
      expect(banner).toHaveTextContent(text);
    },
  );

  it("ChangeRole_MemberGone_RefreshesTheList", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(people, requests),
      changeAnswer(() => problem(404, ErrorCode.MemberNotFound)),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openChangeRole();
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Change role" }),
    );

    await waitFor(() => expect(requests).toHaveLength(2));
  });

  it("ChangeRole_RefusedEscalation_ReadsYourPermissionsAgain", async () => {
    const reads: string[] = [];
    server.use(
      countedTenant(reads),
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      changeAnswer(() => problem(403, ErrorCode.RoleEscalation)),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openChangeRole();
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Change role" }),
    );

    await waitFor(() => expect(reads).toHaveLength(2));
  });

  it("ChangeRole_YourOwnRole_RefreshesYourPermissions", async () => {
    const reads: string[] = [];
    server.use(
      countedTenant(reads),
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      http.patch(`*/v1/tenants/${northwind.id}/members/${ayse.userId}`, () =>
        HttpResponse.json({
          ...ayse,
          role: roleOf.admin,
        }),
      ),
    );

    renderInTenant(<MembersPage />);
    await pickFromRowMenu("Members", "Ayşe Yılmaz", "Change role");
    const dialog = await screen.findByRole("alertdialog");
    await userEvent.click(within(dialog).getByRole("radio", { name: "Admin" }));
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Change role" }),
    );

    await waitFor(() => expect(reads).toHaveLength(2));
  });

  it.each([
    [403, ErrorCode.TenantSuspended],
    [403, ErrorCode.TenantClosed],
    [404, ErrorCode.TenantNotFound],
  ])(
    "ChangeRole_Refused%s_%s_ReadsTheOrganisationAgain",
    async (status, code) => {
      const reads: string[] = [];
      server.use(
        countedTenant(reads),
        ...tenantAnswers(BuiltInRole.Owner),
        membersAnswer(),
        changeAnswer(() => problem(status, code)),
      );

      renderInTenant(<MembersPage />);
      const dialog = await openChangeRole();
      await userEvent.click(
        within(dialog).getByRole("button", { name: "Change role" }),
      );

      await waitFor(() => expect(reads).toHaveLength(2));
    },
  );
});
