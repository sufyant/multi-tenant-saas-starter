import {
  BuiltInRole,
  ErrorCode,
  type InvitationResponse,
  InvitationResponseState,
  Language,
  Permission,
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
  ayse,
  customRole,
  northwind,
  permissionsOf,
  pickFromRowMenu,
  problem,
  renderInTenant,
  roleOf,
  tenantAnswers,
} from "../../../test/tenant";
import { MembersPage } from "./members-page";

vi.mock("@clerk/nextjs", () => ({ useClerk: () => ({ signOut: vi.fn() }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const hour = 60 * 60 * 1000;
const invitation = (
  id: string,
  email: string,
  hoursLeft: number,
  overrides: Partial<InvitationResponse> = {},
): InvitationResponse => ({
  id,
  email,
  role: roleOf.member,
  invitedBy: ayse.userId,
  invitedByName: { firstName: "Ayşe", lastName: "Yılmaz", deleted: false },
  language: Language.En,
  createdAt: new Date().toISOString(),
  expiresAt: new Date(Date.now() + hoursLeft * hour).toISOString(),
  state:
    hoursLeft > 0
      ? InvitationResponseState.Pending
      : InvitationResponseState.Expired,
  ...overrides,
});
// The viewer reads the page in English; Ali's email went out in Arabic and Conor's in Turkish.
const ali = invitation("inv-01", "ali@northwind.io", 6 * 24, {
  language: Language.Ar,
});
const conor = invitation("inv-03", "conor@northwind.io", 20, {
  invitedByName: { firstName: null, lastName: null, deleted: true },
  language: Language.Tr,
});
const padraig = invitation("inv-04", "padraig@northwind.io", -4 * 24);
const owner = invitation("inv-05", "fatma@northwind.io", 3 * 24, {
  role: roleOf.owner,
});

const listAnswer = (
  items: InvitationResponse[] = [ali, conor, padraig, owner],
  requests: URLSearchParams[] = [],
) =>
  http.get(`*/v1/tenants/${northwind.id}/invitations`, ({ request }) => {
    requests.push(new URL(request.url).searchParams);
    return HttpResponse.json({
      items,
      page: 1,
      pageSize: 20,
      total: items.length,
    });
  });

const url = "/northwind/settings/members?tab=invitations";
const rowOf = async (email: string) =>
  (
    await within(
      await screen.findByRole("table", { name: "Invitations" }),
    ).findByText(email)
  ).closest("tr");

describe("InvitationsTable", () => {
  it("Invitations_Statuses_InvitedExpiresSoonAndExpired", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), listAnswer());

    renderInTenant(<MembersPage />, { url });

    expect(await rowOf("ali@northwind.io")).toHaveTextContent("Invited");
    expect(await rowOf("conor@northwind.io")).toHaveTextContent("Expires soon");
    expect(await rowOf("padraig@northwind.io")).toHaveTextContent("Expired");
  });

  it("Invitations_InvitedBy_NamesThePersonOrSaysDeletedUser", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), listAnswer());

    renderInTenant(<MembersPage />, { url });

    expect(await rowOf("ali@northwind.io")).toHaveTextContent("Ayşe Yılmaz");
    expect(await rowOf("conor@northwind.io")).toHaveTextContent("Deleted user");
  });

  it("Invitations_PendingMenu_InviteAgainAndRevoke", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), listAnswer());

    renderInTenant(<MembersPage />, { url });
    await userEvent.click(
      within((await rowOf("ali@northwind.io")) as HTMLElement).getByRole(
        "button",
        { name: "Actions for ali@northwind.io" },
      ),
    );

    expect(
      await screen.findByRole("menuitem", { name: "Invite again" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("menuitem", { name: "Revoke invitation" }),
    ).toBeInTheDocument();
  });

  it("Invitations_ExpiredMenu_InviteAgainAndCancel", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), listAnswer());

    renderInTenant(<MembersPage />, { url });
    await userEvent.click(
      within((await rowOf("padraig@northwind.io")) as HTMLElement).getByRole(
        "button",
        { name: "Actions for padraig@northwind.io" },
      ),
    );

    expect(
      await screen.findByRole("menuitem", { name: "Invite again" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("menuitem", { name: "Cancel invitation" }),
    ).toBeInTheDocument();
  });

  it("Invitations_AdminOnAnOwnerInvitation_CannotInviteAgainAndSaysWhy", async () => {
    server.use(...tenantAnswers(BuiltInRole.Admin), listAnswer());

    renderInTenant(<MembersPage />, { url });
    await userEvent.click(
      within((await rowOf("fatma@northwind.io")) as HTMLElement).getByRole(
        "button",
        { name: "Actions for fatma@northwind.io" },
      ),
    );

    const item = await screen.findByRole("menuitem", { name: /Invite again/ });
    expect(item).toHaveAttribute("aria-disabled", "true");
    expect(item).toHaveTextContent("Only an Owner can invite an Owner.");
  });

  it("Invitations_AdminOnAnOwnerInvitation_CannotRevokeAndSaysWhy", async () => {
    server.use(...tenantAnswers(BuiltInRole.Admin), listAnswer());

    renderInTenant(<MembersPage />, { url });
    await userEvent.click(
      within((await rowOf("fatma@northwind.io")) as HTMLElement).getByRole(
        "button",
        { name: "Actions for fatma@northwind.io" },
      ),
    );

    const item = await screen.findByRole("menuitem", {
      name: /Revoke invitation/,
    });
    expect(item).toHaveAttribute("aria-disabled", "true");
    expect(item).toHaveTextContent("Only an Owner can revoke this invitation.");
  });

  it("Invitations_AdminOnACustomRoleItDoesNotFullyHold_DisablesBothWithTheEscalationText", async () => {
    const roleEditor = customRole(
      "0198f0a2-0000-7000-8000-0000000000c3",
      "Role editor",
      [Permission.TenantProfileRead, Permission.TenantRolesUpdate],
    );
    const niall = invitation("inv-06", "niall@northwind.io", 3 * 24, {
      role: roleEditor,
    });
    server.use(
      ...tenantAnswers(BuiltInRole.Admin, permissionsOf.admin, [roleEditor]),
      listAnswer([niall]),
    );

    renderInTenant(<MembersPage />, { url });
    await userEvent.click(
      within((await rowOf("niall@northwind.io")) as HTMLElement).getByRole(
        "button",
        { name: "Actions for niall@northwind.io" },
      ),
    );

    const items = [
      await screen.findByRole("menuitem", { name: /Invite again/ }),
      screen.getByRole("menuitem", { name: /Revoke invitation/ }),
    ];
    expect(items.map((item) => item.getAttribute("aria-disabled"))).toEqual([
      "true",
      "true",
    ]);
    expect(items[1]).toHaveTextContent(
      "This role has permissions your role doesn’t have.",
    );
  });

  it("Invitations_InviteAgain_SendsANewInvitationWithANewKeyEachTime", async () => {
    const keys: (string | null)[] = [];
    const bodies: unknown[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      listAnswer(),
      http.post(
        `*/v1/tenants/${northwind.id}/invitations`,
        async ({ request }) => {
          keys.push(request.headers.get("Idempotency-Key"));
          bodies.push(await request.json());
          return HttpResponse.json(
            {
              ...invitation("inv-06", "ali@northwind.io", 7 * 24),
              expiresAt: "2026-11-02T10:00:00Z",
            },
            { status: 201 },
          );
        },
      ),
    );

    renderInTenant(<MembersPage />, { url });
    await pickFromRowMenu("Invitations", "ali@northwind.io", "Invite again");
    expect(
      await screen.findByText("ali@northwind.io can join until 2 Nov 2026."),
    ).toBeInTheDocument();
    await pickFromRowMenu("Invitations", "ali@northwind.io", "Invite again");

    await waitFor(() => expect(keys).toHaveLength(2));
    expect(keys[1]).not.toBe(keys[0]);
    expect(bodies[0]).toEqual({
      email: "ali@northwind.io",
      roleId: roleOf.member.id,
      language: Language.Ar,
    });
    expect(screen.getAllByText("Invitation sent again")[0]).toBeInTheDocument();
  });

  it("Invitations_InviteAgainRefused_SaysWhyInAToast", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      listAnswer(),
      http.post(`*/v1/tenants/${northwind.id}/invitations`, () =>
        problem(409, ErrorCode.MemberExists),
      ),
    );

    renderInTenant(<MembersPage />, { url });
    await pickFromRowMenu("Invitations", "ali@northwind.io", "Invite again");

    expect(
      (await screen.findAllByText("Invitation not sent"))[0],
    ).toBeInTheDocument();
    expect(
      screen.getAllByText(/This person is already a member\./)[0],
    ).toBeInTheDocument();
  });

  it("Invitations_Revoke_ConfirmsRevokesAndRefreshes", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      listAnswer(undefined, requests),
      http.delete(
        `*/v1/tenants/${northwind.id}/invitations/${ali.id}`,
        () => new HttpResponse(null, { status: 204 }),
      ),
    );

    renderInTenant(<MembersPage />, { url });
    await pickFromRowMenu(
      "Invitations",
      "ali@northwind.io",
      "Revoke invitation",
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Revoke the invitation to ali@northwind.io?",
    });
    expect(dialog).toHaveTextContent(
      "The link in their email stops working at once.",
    );
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Revoke invitation" }),
    );

    expect(
      (await screen.findAllByText("Invitation revoked"))[0],
    ).toBeInTheDocument();
    expect(requests).toHaveLength(2);
  });

  it("Invitations_CancelExpired_SaysItsLinkAlreadyStopped", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      listAnswer(),
      http.delete(
        `*/v1/tenants/${northwind.id}/invitations/${padraig.id}`,
        () => new HttpResponse(null, { status: 204 }),
      ),
    );

    renderInTenant(<MembersPage />, { url });
    await pickFromRowMenu(
      "Invitations",
      "padraig@northwind.io",
      "Cancel invitation",
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Cancel the expired invitation to padraig@northwind.io?",
    });
    expect(dialog).toHaveTextContent("Its link already stopped working.");
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Cancel invitation" }),
    );

    expect(
      (await screen.findAllByText("Invitation cancelled"))[0],
    ).toBeInTheDocument();
  });

  it("Invitations_RevokeAnAcceptedOne_SaysTheyJoinedAndRefreshes", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      listAnswer(undefined, requests),
      http.delete(`*/v1/tenants/${northwind.id}/invitations/${ali.id}`, () =>
        problem(409, ErrorCode.InvitationNotPending),
      ),
    );

    renderInTenant(<MembersPage />, { url });
    await pickFromRowMenu(
      "Invitations",
      "ali@northwind.io",
      "Revoke invitation",
    );
    const dialog = await screen.findByRole("alertdialog");
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Revoke invitation" }),
    );

    expect(
      (await screen.findAllByText("ali@northwind.io already joined"))[0],
    ).toBeInTheDocument();
    await waitFor(() => expect(requests).toHaveLength(2));
    await waitFor(() =>
      expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument(),
    );
  });

  it("Invitations_RevokeRefusedEscalation_SaysWhyAndRefreshes", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      listAnswer(undefined, requests),
      http.delete(`*/v1/tenants/${northwind.id}/invitations/${ali.id}`, () =>
        problem(403, ErrorCode.RoleEscalation),
      ),
    );

    renderInTenant(<MembersPage />, { url });
    await pickFromRowMenu(
      "Invitations",
      "ali@northwind.io",
      "Revoke invitation",
    );
    const dialog = await screen.findByRole("alertdialog");
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Revoke invitation" }),
    );

    const banner = await within(dialog).findByRole("status");
    expect(banner).toHaveTextContent(
      "This role has permissions your role doesn’t have.",
    );
    expect(banner).toHaveTextContent(
      "Ask an Owner of Northwind Traders to do this.",
    );
    await waitFor(() => expect(requests).toHaveLength(2));
  });

  it("Invitations_None_SaysSoAndOffersToInvite", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), listAnswer([]));

    renderInTenant(<MembersPage />, { url });

    expect(
      await screen.findByRole("heading", { name: "No pending invitations" }),
    ).toBeInTheDocument();
    expect(
      screen.getAllByRole("button", { name: "Invite member" }),
    ).toHaveLength(2);
  });

  it("Invitations_TheUrl_DrivesSearchFilterAndSort", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      listAnswer(undefined, requests),
    );

    renderInTenant(<MembersPage />, {
      url: `${url}&q=ali&roleId=${roleOf.admin.id}&sort=-expiresAt`,
    });

    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests.map((params) => Object.fromEntries(params))).toEqual([
      {
        q: "ali",
        roleId: roleOf.admin.id,
        page: "1",
        sort: "-expiresAt",
      },
    ]);
  });

  it("Invitations_RevokeAGoneOne_SaysItNoLongerExistsAndRefreshes", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      listAnswer(undefined, requests),
      http.delete(`*/v1/tenants/${northwind.id}/invitations/${ali.id}`, () =>
        problem(404, ErrorCode.InvitationNotFound),
      ),
    );

    renderInTenant(<MembersPage />, { url });
    await pickFromRowMenu(
      "Invitations",
      "ali@northwind.io",
      "Revoke invitation",
    );
    const dialog = await screen.findByRole("alertdialog");
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Revoke invitation" }),
    );

    expect(
      (await screen.findAllByText("That invitation no longer exists"))[0],
    ).toBeInTheDocument();
    await waitFor(() => expect(requests).toHaveLength(2));
    await waitFor(() =>
      expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument(),
    );
  });

  it("Invitations_InviteAgainARowInArabic_SendsArabicNotTheViewersEnglish", async () => {
    const bodies: unknown[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      listAnswer(),
      http.post(
        `*/v1/tenants/${northwind.id}/invitations`,
        async ({ request }) => {
          bodies.push(await request.json());
          return HttpResponse.json(
            invitation("inv-07", "ali@northwind.io", 7 * 24, {
              language: Language.Ar,
            }),
            { status: 201 },
          );
        },
      ),
    );

    renderInTenant(<MembersPage />, { url });
    await pickFromRowMenu("Invitations", "ali@northwind.io", "Invite again");

    await waitFor(() => expect(bodies).toHaveLength(1));
    expect(bodies[0]).toMatchObject({ language: Language.Ar });
  });
});
