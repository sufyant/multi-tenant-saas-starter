import { BuiltInRole, ErrorCode, Permission } from "@repo/api-client";
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
  countedMyTenant,
  customRole,
  mehmet,
  northwind,
  permissionsOf,
  planProblem,
  planWith,
  problem,
  renderInTenant,
  roleOf,
  tenantAnswers,
  viewerAnswers,
} from "../../../test/tenant";
import { NewRolePage } from "./new-role-page";
import { RolePage } from "./role-page";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  push.mockReset();
});
afterAll(() => server.close());

const list = "/northwind/settings/roles";
const reviewer = customRole(
  "0198f0a2-0000-7000-8000-0000000000c3",
  "Reviewer",
  [Permission.TenantProfileRead, Permission.TenantMembersRead],
);

type Sent = { body: unknown; key: string | null };

const createAnswer = (answer: () => Response, sent: Sent[] = []) =>
  http.post(`*/v1/tenants/${northwind.id}/roles`, async ({ request }) => {
    sent.push({
      body: await request.json(),
      key: request.headers.get("Idempotency-Key"),
    });
    return answer();
  });

const updateAnswer = (answer: () => Response, sent: unknown[] = []) =>
  http.patch(
    `*/v1/tenants/${northwind.id}/roles/${reviewer.id}`,
    async ({ request }) => {
      sent.push(await request.json());
      return answer();
    },
  );

const created = (name: string) =>
  HttpResponse.json(
    customRole("0198f0a2-0000-7000-8000-0000000000c9", name, []),
    { status: 201 },
  );

const box = (name: string) => screen.findByRole("checkbox", { name });

const openNew = async () => {
  renderInTenant(<NewRolePage />, { url: `${list}/new` });
  return screen.findByRole("textbox", { name: "Name" });
};

const openReviewer = async () => {
  renderInTenant(<RolePage roleId={reviewer.id} />, {
    url: `${list}/${reviewer.id}`,
  });
  return screen.findByRole("textbox", { name: "Name" });
};

describe("RoleEditor rules", () => {
  it("RoleEditor_New_LocksSeeTheOrganisationsDetailsOn", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    await openNew();

    const profile = await box("See the organisation’s details");
    expect(profile).toBeChecked();
    expect(profile).toBeDisabled();
    expect(profile).toHaveAccessibleDescription("Every role has this.");
  });

  it.each([
    "Invite people",
    "Change members’ roles",
    "Remove members",
    "Revoke and cancel invitations",
  ])("RoleEditor_%s_LocksSeeRolesOnUntilNoneIsChecked", async (label) => {
    server.use(...tenantAnswers(BuiltInRole.Owner));
    await openNew();
    const rolesRead = await box("See roles");
    expect(rolesRead).toBeEnabled();

    await userEvent.click(await box(label));

    expect(rolesRead).toBeChecked();
    expect(rolesRead).toBeDisabled();
    expect(rolesRead).toHaveAccessibleDescription(
      "Needed to invite people, change roles, remove members and revoke invitations.",
    );

    await userEvent.click(await box(label));

    expect(rolesRead).toBeEnabled();
    expect(rolesRead).toBeChecked();
  });

  it("RoleEditor_PermissionTheViewerLacks_IsDisabledWithTheReason", async () => {
    const creator = customRole(
      "0198f0a2-0000-7000-8000-0000000000c5",
      "Role creator",
      [
        Permission.TenantProfileRead,
        Permission.TenantMembersRead,
        Permission.TenantRolesRead,
        Permission.TenantRolesCreate,
      ],
    );
    server.use(
      ...viewerAnswers(creator, creator.permissions, mehmet, [creator]),
    );

    await openNew();

    const remove = await box("Remove members");
    expect(remove).toBeDisabled();
    expect(remove).toHaveAccessibleDescription(
      "Your role doesn’t have this permission.",
    );
    expect(await box("See members")).toBeEnabled();
  });

  it("RoleEditor_Owner_NeverShowsManageOwners", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));

    await openNew();

    await box("See members");
    expect(
      screen.queryByRole("checkbox", { name: "Manage Owners" }),
    ).not.toBeInTheDocument();
  });

  it.each([
    ["ADMIN", "A role named ADMIN already exists."],
    ["  billing LEAD ", "A role named billing LEAD already exists."],
  ])(
    "RoleEditor_TakenName_%s_ShowsTheFieldErrorWithoutARequest",
    async (name, error) => {
      const sent: Sent[] = [];
      server.use(
        ...tenantAnswers(BuiltInRole.Owner, undefined, [billingLead]),
        createAnswer(() => created(name), sent),
      );
      const field = await openNew();

      await userEvent.type(field, name);
      await userEvent.click(
        screen.getByRole("button", { name: "Create role" }),
      );

      expect(await screen.findByText(error)).toBeInTheDocument();
      expect(sent).toHaveLength(0);
    },
  );
});

describe("CreateRole", () => {
  it("CreateRole_Success_SendsTheTrimmedNameAndTheRequiredPermissionsWithAKey", async () => {
    const sent: Sent[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      createAnswer(() => created("Support"), sent),
    );
    const field = await openNew();

    await userEvent.type(field, "  Support ");
    await userEvent.click(await box("Invite people"));
    await userEvent.click(screen.getByRole("button", { name: "Create role" }));

    expect(await screen.findByText("Role created")).toBeInTheDocument();
    expect(screen.getByText("Support is ready to give.")).toBeInTheDocument();
    expect(sent).toEqual([
      {
        body: {
          name: "Support",
          permissions: [
            Permission.TenantProfileRead,
            Permission.TenantInvitationsCreate,
            Permission.TenantRolesRead,
          ],
        },
        key: expect.stringMatching(/^[\da-f-]{36}$/),
      },
    ]);
    expect(push).toHaveBeenCalledWith(list);
  });

  it.each([
    [409, ErrorCode.RoleNameTaken, "A role named Support already exists."],
    [400, ErrorCode.RoleNameInvalid, "Enter a name."],
  ])(
    "CreateRole_Refused%s_%s_ShowsTheFieldError",
    async (status, code, text) => {
      server.use(
        ...tenantAnswers(BuiltInRole.Owner),
        createAnswer(() => problem(status, code)),
      );
      const field = await openNew();

      await userEvent.type(field, "Support");
      await userEvent.click(
        screen.getByRole("button", { name: "Create role" }),
      );

      expect(await screen.findByText(text)).toBeInTheDocument();
      expect(field).toHaveAccessibleDescription(text);
    },
  );

  it.each([
    ["required", "Enter a name."],
    ["too_long", "Use 1 to 100 characters."],
  ])(
    "CreateRole_RefusedValidationFailed_%s_ShowsTheFieldError",
    async (fieldCode, text) => {
      server.use(
        ...tenantAnswers(BuiltInRole.Owner),
        createAnswer(() =>
          HttpResponse.json(
            {
              status: 400,
              title: "Refused",
              code: ErrorCode.RequestValidationFailed,
              traceId:
                "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
              errors: { name: [fieldCode] },
            },
            { status: 400 },
          ),
        ),
      );
      const field = await openNew();

      await userEvent.type(field, "Support");
      await userEvent.click(
        screen.getByRole("button", { name: "Create role" }),
      );

      expect(await screen.findByText(text)).toBeInTheDocument();
    },
  );

  it.each([
    [400, ErrorCode.RolePermissionsRequired, "Choose at least one permission."],
    [
      400,
      ErrorCode.RoleProfileReadRequired,
      "Every role must see the organisation’s details.",
    ],
    [
      400,
      ErrorCode.RoleRolesReadRequired,
      "A role that invites people, changes or removes a member, or revokes an invitation must be able to read the roles.",
    ],
    [
      422,
      ErrorCode.RolePermissionOwnerOnly,
      "Manage Owners belongs to the Owner role only.",
    ],
    [
      400,
      ErrorCode.RolePermissionInvalid,
      "One of these permissions doesn’t exist any more. Reload the page.",
    ],
    [403, ErrorCode.RoleEscalation, "Your role doesn’t have this permission."],
    [
      409,
      ErrorCode.PlanLimitReached,
      "This organisation has reached a limit of its plan.",
    ],
  ])("CreateRole_Refused%s_%s_ShowsTheBanner", async (status, code, text) => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      createAnswer(() => problem(status, code)),
    );
    const field = await openNew();

    await userEvent.type(field, "Support");
    await userEvent.click(screen.getByRole("button", { name: "Create role" }));

    expect(await screen.findByRole("status")).toHaveTextContent(text);
  });

  it("CreateRole_RefusedLimitReached_ShowsTheReasonOfTheLimit", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "roles.custom.max": 20 }, { "roles.custom.max": 19 }),
      ),
      createAnswer(() =>
        planProblem(409, ErrorCode.PlanLimitReached, "roles.custom.max"),
      ),
    );
    const field = await openNew();

    await userEvent.type(field, "Support");
    await userEvent.click(screen.getByRole("button", { name: "Create role" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Your organisation’s plan allows 20 custom roles. Delete one to create another.",
    );
  });

  it("CreateRole_RefusedLimitReachedOfAnUnknownKey_ShowsTheNeutralText", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      createAnswer(() =>
        planProblem(409, ErrorCode.PlanLimitReached, "storage.max"),
      ),
    );
    const field = await openNew();

    await userEvent.type(field, "Support");
    await userEvent.click(screen.getByRole("button", { name: "Create role" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "This organisation has reached a limit of its plan.",
    );
  });

  it("CreateRole_RefusedFeatureUnavailable_ShowsTheReasonOfTheFeature", async () => {
    // The gap 32-backend left: the editor had no text for plan.feature_unavailable.
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      createAnswer(() =>
        planProblem(403, ErrorCode.PlanFeatureUnavailable, "roles.custom"),
      ),
    );
    const field = await openNew();

    await userEvent.type(field, "Support");
    await userEvent.click(screen.getByRole("button", { name: "Create role" }));

    const banner = await screen.findByRole("status");
    expect(banner).toHaveTextContent(
      "Your organisation’s plan doesn’t include custom roles.",
    );
    expect(banner).not.toHaveTextContent("Couldn’t");
  });

  it("CreateRole_RefusedFeatureUnavailableOfAnUnknownKey_ShowsTheNeutralText", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      createAnswer(() =>
        planProblem(403, ErrorCode.PlanFeatureUnavailable, "reports.export"),
      ),
    );
    const field = await openNew();

    await userEvent.type(field, "Support");
    await userEvent.click(screen.getByRole("button", { name: "Create role" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "This organisation's plan does not include this feature.",
    );
  });

  it("CreateRole_RefusedByThePlan_ReadsGetMyTenantAgain", async () => {
    const reads = { count: 0 };
    server.use(
      countedMyTenant(reads),
      ...tenantAnswers(BuiltInRole.Owner),
      createAnswer(() =>
        planProblem(403, ErrorCode.PlanFeatureUnavailable, "roles.custom"),
      ),
    );
    const field = await openNew();
    await waitFor(() => expect(reads.count).toBe(1));

    await userEvent.type(field, "Support");
    await userEvent.click(screen.getByRole("button", { name: "Create role" }));

    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("CreateRole_RefusedNameTaken_ReadsTheRolesAgain", async () => {
    let reads = 0;
    server.use(
      http.get(`*/v1/tenants/${northwind.id}/roles`, () => {
        reads += 1;
        return HttpResponse.json({
          items: [],
          page: 1,
          pageSize: 100,
          total: 0,
        });
      }),
      ...tenantAnswers(BuiltInRole.Owner),
      createAnswer(() => problem(409, ErrorCode.RoleNameTaken)),
    );
    const field = await openNew();

    await userEvent.type(field, "Support");
    await userEvent.click(screen.getByRole("button", { name: "Create role" }));

    await waitFor(() => expect(reads).toBe(2));
  });

  it("CreateRole_SentAgainAfterARefusal_SendsANewKey", async () => {
    const sent: Sent[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      createAnswer(() => problem(409, ErrorCode.PlanLimitReached), sent),
    );
    const field = await openNew();
    await userEvent.type(field, "Support");
    const create = screen.getByRole("button", { name: "Create role" });

    await userEvent.click(create);
    await screen.findByRole("status");
    await userEvent.click(create);

    await waitFor(() => expect(sent).toHaveLength(2));
    expect(sent[0]?.key).not.toBe(sent[1]?.key);
  });
});

describe("NewRolePage", () => {
  it("NewRolePage_CustomRolesOff_ShowsTheReasonAndNoForm", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "roles.custom": false, "roles.custom.max": 0 }),
      ),
    );

    renderInTenant(<NewRolePage />, { url: `${list}/new` });

    expect(
      await screen.findByText(
        "Your organisation’s plan doesn’t include custom roles.",
      ),
    ).toBeVisible();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Back to Roles" }),
    ).toHaveAttribute("href", list);
  });

  it("NewRolePage_AtTheLimit_ShowsTheReasonAndNoForm", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "roles.custom.max": 2 }, { "roles.custom.max": 2 }),
      ),
    );

    renderInTenant(<NewRolePage />, { url: `${list}/new` });

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Your organisation’s plan allows 2 custom roles. Delete one to create another.",
    );
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
  });

  it("NewRolePage_CreatesTheLastRoleAllowed_StillLeavesForTheList", async () => {
    // The success reads GetMyTenant again, which then says full: the page that opened with room keeps its editor until it leaves.
    let created = false;
    server.use(
      http.get(`*/v1/tenants/${northwind.id}`, () =>
        HttpResponse.json({
          ...northwind,
          role: roleOf.owner,
          createdAt: "2026-01-05T09:00:00Z",
          permissions: permissionsOf.owner,
          ...planWith(
            { "roles.custom.max": 1 },
            { "roles.custom.max": Number(created) },
          ),
        }),
      ),
      ...tenantAnswers(BuiltInRole.Owner),
      createAnswer(() => {
        created = true;
        return HttpResponse.json(reviewer, { status: 201 });
      }),
    );
    const field = await openNew();

    await userEvent.type(field, "Support");
    await userEvent.click(screen.getByRole("button", { name: "Create role" }));

    await waitFor(() => expect(push).toHaveBeenCalledWith(list));
  });
});

describe("UpdateRole", () => {
  it("UpdateRole_RefusedFeatureUnavailable_ShowsTheReasonOfTheFeature", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]),
      updateAnswer(() =>
        planProblem(403, ErrorCode.PlanFeatureUnavailable, "roles.custom"),
      ),
    );
    const field = await openReviewer();

    await userEvent.type(field, " team");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Your organisation’s plan doesn’t include custom roles.",
    );
  });

  it("UpdateRole_Success_SendsTheNameAndEveryPermissionAfterwards", async () => {
    const sent: unknown[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]),
      updateAnswer(() => HttpResponse.json(reviewer), sent),
    );
    const field = await openReviewer();

    await userEvent.clear(field);
    await userEvent.type(field, "Auditor");
    await userEvent.click(await box("See Activity"));
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    expect(await screen.findByText("Changes saved.")).toBeInTheDocument();
    expect(sent).toEqual([
      {
        name: "Auditor",
        permissions: [
          Permission.TenantProfileRead,
          Permission.TenantMembersRead,
          Permission.TenantAuditRead,
        ],
      },
    ]);
    expect(push).toHaveBeenCalledWith(list);
  });

  it.each([
    [409, ErrorCode.RoleBuiltIn, "Built-in roles can’t be edited or deleted."],
    [403, ErrorCode.RoleEscalation, "Your role doesn’t have this permission."],
  ])(
    "UpdateRole_Refused%s_%s_ShowsTheBannerAndReadsTheRolesAgain",
    async (status, code, text) => {
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
        updateAnswer(() => problem(status, code)),
      );
      await openReviewer();

      await userEvent.click(await box("See Activity"));
      await userEvent.click(
        screen.getByRole("button", { name: "Save changes" }),
      );

      expect(await screen.findByRole("status")).toHaveTextContent(text);
      await waitFor(() => expect(reads).toBeGreaterThan(1));
    },
  );

  it("UpdateRole_RefusedNotFound_ToastsAndGoesToTheList", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]),
      updateAnswer(() => problem(404, ErrorCode.RoleNotFound)),
    );
    await openReviewer();

    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    expect(
      await screen.findByText("This role no longer exists."),
    ).toBeInTheDocument();
    expect(push).toHaveBeenCalledWith(list);
  });

  it("UpdateRole_RefusedNotPermitted_Toasts", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]),
      updateAnswer(() => problem(403, ErrorCode.RoleNotPermitted)),
    );
    await openReviewer();

    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    expect(
      await screen.findByText("Your role can’t change roles."),
    ).toBeInTheDocument();
  });
});

describe("Editor pages", () => {
  it("RoleEditor_DeleteFromTheEditor_GoesToTheListWithoutAsking", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]),
      http.delete(
        `*/v1/tenants/${northwind.id}/roles/${reviewer.id}`,
        () => new HttpResponse(null, { status: 204 }),
      ),
    );
    await openReviewer();

    await userEvent.click(await box("See Activity"));
    await userEvent.click(screen.getByRole("button", { name: "Delete role" }));
    const dialog = await screen.findByRole("alertdialog", {
      name: "Delete Reviewer?",
    });
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Delete role" }),
    );

    expect(await screen.findByText("Role deleted")).toBeInTheDocument();
    expect(push).toHaveBeenCalledWith(list);
    expect(
      screen.queryByRole("alertdialog", {
        name: "Discard changes to Reviewer?",
      }),
    ).not.toBeInTheDocument();
  });

  it("RolePage_CustomRoleOutOfReach_ReadsWithoutAForm", async () => {
    const roleEditor = customRole(
      "0198f0a2-0000-7000-8000-0000000000c4",
      "Role editor",
      [
        Permission.TenantProfileRead,
        Permission.TenantMembersRead,
        Permission.TenantRolesRead,
        Permission.TenantRolesUpdate,
      ],
    );
    server.use(
      ...viewerAnswers(roleEditor, roleEditor.permissions, mehmet, [
        roleEditor,
        billingLead,
      ]),
    );

    renderInTenant(<RolePage roleId={billingLead.id} />, {
      url: `${list}/${billingLead.id}`,
    });

    expect(
      await screen.findByRole("heading", { name: "Billing lead" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    expect(await box("See Activity")).toBeDisabled();
  });

  it("NewRolePage_WithoutRolesCreate_ShowsPermissionDenied", async () => {
    server.use(...tenantAnswers(BuiltInRole.Admin));

    renderInTenant(<NewRolePage />, { url: `${list}/new` });

    expect(
      await screen.findByRole("heading", { name: "You can’t open New role" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
  });
});

describe("Leaving with changes", () => {
  it("LeaveEditor_LinkWithChanges_AsksAndDiscardGoesOn", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]));
    await openReviewer();

    await userEvent.click(await box("See Activity"));
    await userEvent.click(screen.getByRole("link", { name: "Members" }));

    expect(
      await screen.findByRole("alertdialog", {
        name: "Discard changes to Reviewer?",
      }),
    ).toHaveTextContent(
      "Your changes are not saved. You can keep editing and save them.",
    );
    expect(push).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole("button", { name: "Discard" }));

    expect(push).toHaveBeenCalledWith("/northwind/settings/members");
  });

  it("LeaveEditor_KeepEditing_StaysWithTheChanges", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]));
    await openReviewer();

    await userEvent.click(await box("See Activity"));
    await userEvent.click(screen.getByRole("link", { name: "Members" }));
    await userEvent.click(
      await screen.findByRole("button", { name: "Keep editing" }),
    );

    await waitFor(() =>
      expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument(),
    );
    expect(push).not.toHaveBeenCalled();
    expect(await box("See Activity")).toBeChecked();
  });

  it("LeaveEditor_BackAfterAnUndoThenNewChanges_StillAsks", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner, undefined, [reviewer]));
    await openReviewer();
    const activity = await box("See Activity");

    await userEvent.click(activity);
    await userEvent.click(activity);
    window.history.back();
    await new Promise((settled) =>
      window.addEventListener("popstate", settled, { once: true }),
    );
    await userEvent.click(activity);
    window.history.back();

    expect(
      await screen.findByRole("alertdialog", {
        name: "Discard changes to Reviewer?",
      }),
    ).toBeInTheDocument();
  });

  it("LeaveEditor_BackWithChanges_Asks", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner));
    const field = await openNew();

    await userEvent.type(field, "Support");
    window.history.back();

    expect(
      await screen.findByRole("alertdialog", {
        name: "Discard the new role?",
      }),
    ).toBeInTheDocument();
  });
});
