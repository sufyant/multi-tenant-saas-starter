import { BuiltInRole, ErrorCode, Permission } from "@repo/api-client";
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
  ayse,
  billingLead,
  customRole,
  ibrahim,
  john,
  mehmet,
  membersAnswer,
  northwind,
  people,
  permissionsOf,
  planWith,
  problem,
  renderInTenant,
  roleOf,
  supportLead,
  tenantAnswers,
  viewerAnswers,
} from "../../../test/tenant";
import { MembersPage } from "./members-page";

vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({ signOut: vi.fn() }),
  useUser: () => ({ user: null }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const invitationsAnswer = (requests: URLSearchParams[] = []) =>
  http.get(`*/v1/tenants/${northwind.id}/invitations`, ({ request }) => {
    requests.push(new URL(request.url).searchParams);
    return HttpResponse.json({ items: [], page: 1, pageSize: 20, total: 0 });
  });

const table = async () =>
  within(await screen.findByRole("table", { name: "Members" }));

const openMenu = async (name: string) => {
  await userEvent.click(
    await (await table()).findByRole("button", { name: `Actions for ${name}` }),
  );
};

describe("MembersPage", () => {
  it("MembersPage_Owner_SeesInviteTabsAndEveryAction", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);

    expect(
      await screen.findByRole("button", { name: "Invite member" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("tab", { name: "Invitations" }),
    ).toBeInTheDocument();
    await openMenu("John Byrne");
    expect(
      await screen.findByRole("menuitem", { name: "Change role" }),
    ).not.toHaveAttribute("aria-disabled");
    expect(
      screen.getByRole("menuitem", { name: "Remove from organisation" }),
    ).not.toHaveAttribute("aria-disabled");
  });

  it("MembersPage_Owner_CannotRemoveThemselvesAndSaysWhy", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);
    await openMenu("Ayşe Yılmaz");

    const remove = await screen.findByRole("menuitem", {
      name: /Remove from organisation/,
    });
    expect(remove).toHaveAttribute("aria-disabled", "true");
    expect(remove).toHaveTextContent(
      "You cannot remove yourself. Ask another Owner or Admin.",
    );
    expect(
      screen.getByRole("menuitem", { name: "Change role" }),
    ).not.toHaveAttribute("aria-disabled");
  });

  it("MembersPage_YourOwnRow_SaysYouInMutedTextAfterTheName", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);

    const name = await (await table()).findByText("Ayşe Yılmaz");
    const you = within(name.closest("tr") as HTMLElement).getByText("(you)");
    expect(you).toHaveClass("text-muted-foreground");
    expect(name.compareDocumentPosition(you)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
  });

  it("MembersPage_NoRoleFilter_NamesAllRoles", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);

    expect(
      await screen.findByRole("combobox", { name: "Filter by role" }),
    ).toHaveTextContent(/^All roles$/);
  });

  it("MembersPage_Admin_CannotTouchAnOwnerAndSaysWhy", async () => {
    server.use(...tenantAnswers(BuiltInRole.Admin), membersAnswer());

    renderInTenant(<MembersPage />);
    await openMenu("Ayşe Yılmaz");

    const change = await screen.findByRole("menuitem", { name: /Change role/ });
    expect(change).toHaveAttribute("aria-disabled", "true");
    expect(change).toHaveTextContent("Only an Owner can change an Owner.");
    expect(
      screen.getByRole("menuitem", { name: /Remove from organisation/ }),
    ).toHaveTextContent("Only an Owner can remove an Owner.");
  });

  it("MembersPage_CustomViewer_ActsOnlyOnMembersWhoseRoleItFullyHolds", async () => {
    const dara = {
      ...john,
      firstName: "Dara",
      lastName: "Quinn",
      role: billingLead,
    };
    server.use(
      ...viewerAnswers(
        supportLead,
        supportLead.permissions,
        { ...mehmet, role: supportLead },
        [supportLead, billingLead],
      ),
      membersAnswer([ayse, { ...mehmet, role: supportLead }, ibrahim, dara]),
    );

    renderInTenant(<MembersPage />);
    await openMenu("Dara Quinn");

    const change = await screen.findByRole("menuitem", { name: /Change role/ });
    expect(change).toHaveAttribute("aria-disabled", "true");
    expect(change).toHaveTextContent(
      "This role has permissions your role doesn’t have.",
    );
    expect(
      screen.queryByRole("menuitem", { name: /Remove from organisation/ }),
    ).not.toBeInTheDocument();
  });

  it("MembersPage_CustomViewerOnAMember_CanChangeTheRole", async () => {
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
    await openMenu("John Byrne");

    expect(
      await screen.findByRole("menuitem", { name: "Change role" }),
    ).not.toHaveAttribute("aria-disabled");
  });

  it("MembersPage_ViewerWhoCannotReadRoles_DisablesRemoveAndSaysWhy", async () => {
    const remover = customRole(
      "0198f0a2-0000-7000-8000-0000000000c4",
      "Remover",
      [
        Permission.TenantProfileRead,
        Permission.TenantMembersRead,
        Permission.TenantMembersDelete,
      ],
    );
    server.use(
      ...viewerAnswers(
        remover,
        remover.permissions,
        { ...mehmet, role: remover },
        [remover],
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);
    await openMenu("John Byrne");

    const remove = await screen.findByRole("menuitem", {
      name: /Remove from organisation/,
    });
    expect(remove).toHaveAttribute("aria-disabled", "true");
    expect(remove).toHaveTextContent(
      "Your role can’t read roles, so it can’t do this.",
    );
  });

  it("MembersPage_ARoleTheListNoLongerHas_DisablesTheActionsAndSaysSo", async () => {
    const gone = {
      ...john,
      role: {
        id: "0198f0a2-0000-7000-8000-0000000000ff",
        name: "Retired role",
        builtIn: false,
      },
    };
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer([ayse, gone]),
    );

    renderInTenant(<MembersPage />);
    await openMenu("John Byrne");

    const change = await screen.findByRole("menuitem", { name: /Change role/ });
    expect(change).toHaveAttribute("aria-disabled", "true");
    expect(change).toHaveTextContent("This role no longer exists.");
  });

  it("MembersPage_FilteredByRole_SendsTheRoleIdAndNamesTheRole", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, permissionsOf.owner, [billingLead]),
      membersAnswer([], requests),
    );

    renderInTenant(<MembersPage />, {
      url: `/northwind/settings/members?roleId=${billingLead.id}`,
    });

    await waitFor(() =>
      expect(requests.at(-1)?.get("roleId")).toBe(billingLead.id),
    );
    await waitFor(() =>
      expect(
        screen.getByRole("combobox", { name: "Filter by role" }),
      ).toHaveTextContent("Role: Billing lead"),
    );
  });

  it("MembersPage_ClearTheRoleFilter_RemovesItFromTheUrlAndTheRequest", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, permissionsOf.owner, [billingLead]),
      membersAnswer(people, requests),
    );

    renderInTenant(<MembersPage />, {
      url: `/northwind/settings/members?roleId=${billingLead.id}`,
    });
    await userEvent.click(
      await screen.findByRole("button", { name: "Clear filter" }),
    );

    await waitFor(() => expect(window.location.search).toBe(""));
    await waitFor(() => expect(requests.at(-1)?.has("roleId")).toBe(false));
    expect(
      screen.queryByRole("button", { name: "Clear filter" }),
    ).not.toBeInTheDocument();
  });

  it("MembersPage_Member_SeesAReadOnlyListWithoutTabsOrInvite", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member), membersAnswer());

    renderInTenant(<MembersPage />);

    expect(await (await table()).findByText("John Byrne")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Invite member" }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole("tablist")).not.toBeInTheDocument();
    expect(
      (await table()).queryByRole("button", { name: /Actions for/ }),
    ).not.toBeInTheDocument();
  });

  it("MembersPage_MemberOpensTheInvitationsTab_SeesTheMembers", async () => {
    const invitationRequests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Member),
      membersAnswer(),
      invitationsAnswer(invitationRequests),
    );

    renderInTenant(<MembersPage />, {
      url: "/northwind/settings/members?tab=invitations",
    });

    expect(await (await table()).findByText("John Byrne")).toBeInTheDocument();
    expect(invitationRequests).toEqual([]);
  });

  it("MembersPage_NoReadPermission_SaysPermissionDenied", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Member, [Permission.TenantProfileRead]),
    );

    renderInTenant(<MembersPage />);

    expect(
      await screen.findByRole("heading", { name: "You can’t open Members" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Back to Overview" }),
    ).toHaveAttribute("href", "/northwind");
  });

  it("MembersPage_DeletedUser_SaysDeletedUser", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer([
        ayse,
        {
          ...john,
          firstName: null,
          lastName: null,
          email: null,
          deleted: true,
        },
      ]),
    );

    renderInTenant(<MembersPage />);

    expect(
      await (await table()).findByText("Deleted user"),
    ).toBeInTheDocument();
  });

  it("MembersPage_TheUrl_DrivesSearchFilterPageAndSort", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(people, requests),
    );

    renderInTenant(<MembersPage />, {
      url: `/northwind/settings/members?q=byrne&roleId=${roleOf.member.id}&page=2&sort=-lastName`,
    });

    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests.map((params) => Object.fromEntries(params))).toEqual([
      {
        q: "byrne",
        roleId: roleOf.member.id,
        page: "2",
        sort: "-lastName",
      },
    ]);
    expect(
      screen.getByRole("searchbox", { name: "Search by name or email" }),
    ).toHaveValue("byrne");
  });

  it("MembersPage_TypeASearch_WritesItToTheUrlAfterAPause", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);
    await userEvent.type(
      await screen.findByRole("searchbox", { name: "Search by name or email" }),
      "john",
    );

    expect(window.location.search).toBe("");
    await waitFor(() => expect(window.location.search).toBe("?q=john"));
  });

  it("MembersPage_SortByName_WritesTheSortToTheUrl", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);
    await userEvent.click(
      await (await table()).findByRole("button", { name: "Name" }),
    );

    await waitFor(() => expect(window.location.search).toBe("?sort=lastName"));
  });

  it("MembersPage_BackAndForward_RestoreTheTab", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />);
    await userEvent.click(
      await screen.findByRole("tab", { name: "Invitations" }),
    );
    await waitFor(() =>
      expect(screen.getByRole("tab", { name: "Invitations" })).toHaveAttribute(
        "aria-selected",
        "true",
      ),
    );

    window.history.back();
    await waitFor(() =>
      expect(screen.getByRole("tab", { name: "Members" })).toHaveAttribute(
        "aria-selected",
        "true",
      ),
    );
    window.history.forward();
    await waitFor(() =>
      expect(screen.getByRole("tab", { name: "Invitations" })).toHaveAttribute(
        "aria-selected",
        "true",
      ),
    );
  });

  it("MembersPage_SearchFindsNothing_OffersToClearIt", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer([]));

    renderInTenant(<MembersPage />, {
      url: "/northwind/settings/members?q=nobody",
    });

    expect(
      await screen.findByRole("heading", { name: "No members match “nobody”" }),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Clear search" }));
    await waitFor(() => expect(window.location.search).toBe(""));
  });

  it("MembersPage_ListFails_ShowsTheReferenceAndTryAgain", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      http.get(`*/v1/tenants/${northwind.id}/members`, () =>
        problem(500, ErrorCode.ServerError),
      ),
    );

    renderInTenant(<MembersPage />);

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Couldn’t load members");
    expect(alert).toHaveTextContent(
      /Ref 4bf92f3577b34da6a3ce929d0e0e4736 · \d\d:\d\d UTC/,
    );
    expect(
      within(alert).getByRole("button", { name: "Try again" }),
    ).toBeInTheDocument();
  });

  it("MembersPage_SuspendedUser_ShowsTheSuspendedScreen", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      http.get(`*/v1/tenants/${northwind.id}/members`, () =>
        problem(403, ErrorCode.UserSuspended),
      ),
    );

    renderInTenant(<MembersPage />);

    expect(
      await screen.findByRole("heading", { name: "Your account is suspended" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Try again" }),
    ).not.toBeInTheDocument();
  });

  it("MembersPage_UnderTheLimit_CountsMembersWithInvitationsAndSaysSo", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 15 }, { "members.max": 10 }),
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);

    expect(
      await screen.findByText("10 of 15 members, invitations included"),
    ).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Invite member" }),
    ).not.toBeDisabled();
    await table();
    expect(screen.queryByText(/your plan allows/)).not.toBeInTheDocument();
  });

  it("MembersPage_Unlimited_ShowsNoCount", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": null }, { "members.max": 10 }),
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);

    await table();
    expect(screen.queryByText(/invitations included/)).not.toBeInTheDocument();
    expect(screen.queryByText(/null/)).not.toBeInTheDocument();
  });

  it("MembersPage_AtTheLimit_DisablesInviteWithTheReason", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 10 }, { "members.max": 10 }),
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);

    const reason =
      "Your organisation’s plan allows 10 members, invitations included. Remove a member or cancel an invitation to invite someone.";
    const invite = await screen.findByRole("button", { name: "Invite member" });
    expect(invite).toBeDisabled();
    expect(invite).toHaveAccessibleDescription(reason);
    expect(screen.getByText(reason)).toBeVisible();
    await table();
    expect(screen.queryByText(/your plan allows/)).not.toBeInTheDocument();
    // At the limit, removing stays allowed.
    await openMenu("John Byrne");
    expect(
      await screen.findByRole("menuitem", { name: "Remove from organisation" }),
    ).not.toHaveAttribute("aria-disabled");
  });

  it("MembersPage_OverTheLimitForAnOwner_ShowsTheBannerToMakeRoom", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 5 }, { "members.max": 10 }),
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);

    const banner = await screen.findByRole("status");
    expect(banner).toHaveTextContent(
      "You have 10 members and pending invitations; your plan allows 5. Remove members or cancel invitations to invite new people. Nobody loses access.",
    );
    expect(
      within(banner).getByRole("link", { name: "Contact us" }),
    ).toHaveAttribute("href", brand.supportUrl);
    expect(
      screen.getByText("10 of 5 members, invitations included"),
    ).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Invite member" }),
    ).toBeDisabled();
  });

  it("MembersPage_OverTheLimitForAnInviterWhoCannotRemove_SaysWhoCanMakeRoom", async () => {
    const inviter = customRole(
      "0198f0a2-0000-7000-8000-0000000000c5",
      "Inviter",
      [
        Permission.TenantProfileRead,
        Permission.TenantMembersRead,
        Permission.TenantInvitationsRead,
        Permission.TenantInvitationsCreate,
        Permission.TenantRolesRead,
      ],
    );
    server.use(
      ...viewerAnswers(
        inviter,
        inviter.permissions,
        { ...mehmet, role: inviter },
        [inviter],
        planWith({ "members.max": 5 }, { "members.max": 10 }),
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);

    expect(await screen.findByRole("status")).toHaveTextContent(
      "You have 10 members and pending invitations; your plan allows 5. Someone who can remove members can make room. Nobody loses access.",
    );
  });

  it("MembersPage_OverTheLimitForAMember_ShowsNoBannerAndNoCount", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Member,
        undefined,
        [],
        planWith({ "members.max": 5 }, { "members.max": 10 }),
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);

    await table();
    expect(screen.queryByText(/your plan allows/)).not.toBeInTheDocument();
    expect(screen.queryByText(/invitations included/)).not.toBeInTheDocument();
  });

  it("MembersPage_ArabicAtTheLimit_ShowsTheReason", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 10 }, { "members.max": 10 }),
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />, { locale: "ar" });

    expect(
      await screen.findByText(
        "تسمح خطة مؤسستك بـ10 من الأعضاء، بما في ذلك الدعوات. أزل عضوًا أو ألغِ دعوة لدعوة شخص.",
      ),
    ).toBeVisible();
  });
});
