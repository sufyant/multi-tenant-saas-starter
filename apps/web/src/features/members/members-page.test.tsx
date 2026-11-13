import {
  BuiltInRole,
  ErrorCode,
  type MemberResponse,
  Permission,
} from "@repo/api-client";
import { brand } from "@repo/brand";
import { browserTimeZone } from "@repo/ui/lib/format-date-time";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { delay, HttpResponse, http } from "msw";
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
import { savedFiles } from "../../../test/download";
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

// clerk-js as loaded: useFreshRouter waits for it before a navigation, and asks no session for a token.
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({
    signOut: vi.fn(),
    on: (_: string, handler: (status: string) => void) => handler("ready"),
    off: vi.fn(),
    session: null,
  }),
  useUser: () => ({ user: null }),
}));

// The invitations' count reads ListInvitations for whom may read them; a test that looks at the invitations answers its own.
const server = setupServer(
  http.get(`*/v1/tenants/${northwind.id}/invitations`, () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 20, total: 0 }),
  ),
);
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  vi.restoreAllMocks();
});
afterAll(() => server.close());

const invitationsAnswer = (requests: URLSearchParams[] = [], total = 0) =>
  http.get(`*/v1/tenants/${northwind.id}/invitations`, ({ request }) => {
    requests.push(new URL(request.url).searchParams);
    return HttpResponse.json({ items: [], page: 1, pageSize: 20, total });
  });

/** The list's own requests, without the one item that the tab's count reads. */
const listRequests = (requests: URLSearchParams[]) =>
  requests.filter((params) => params.get("pageSize") !== "1");

const person = (index: number): MemberResponse => ({
  ...john,
  userId: `0198f0a2-0000-7000-8000-1${String(index).padStart(11, "0")}`,
  name: `Person ${index}`,
  email: `person${index}@northwind.io`,
});

/** ListMembers of `count` people, each page of the size the request asks for. */
const pagedMembersAnswer = (count: number, requests: URLSearchParams[] = []) =>
  http.get(`*/v1/tenants/${northwind.id}/members`, ({ request }) => {
    const params = new URL(request.url).searchParams;
    requests.push(params);
    const page = Number(params.get("page") ?? 1);
    const pageSize = Number(params.get("pageSize") ?? 20);
    const all = Array.from({ length: count }, (_, index) => person(index + 1));
    return HttpResponse.json({
      items: all.slice((page - 1) * pageSize, page * pageSize),
      page,
      pageSize,
      total: count,
    });
  });

/** ListMembers of two people, one on each page; the second page never comes. */
const secondPageNeverComesAnswer = () =>
  http.get(`*/v1/tenants/${northwind.id}/members`, async ({ request }) => {
    if (new URL(request.url).searchParams.get("page") === "2")
      await delay("infinite");
    return HttpResponse.json({
      items: [person(1)],
      page: 1,
      pageSize: 1,
      total: 2,
    });
  });

/** ListMembers: every person without a search, John alone with one. */
const searchedMembersAnswer = () =>
  http.get(`*/v1/tenants/${northwind.id}/members`, ({ request }) => {
    const items = new URL(request.url).searchParams.has("q") ? [john] : people;
    return HttpResponse.json({
      items,
      page: 1,
      pageSize: 20,
      total: items.length,
    });
  });

const table = async () =>
  within(await screen.findByRole("table", { name: "Members" }));

const openMenu = async (name: string) => {
  await userEvent.click(
    await (await table()).findByRole("button", { name: `Actions for ${name}` }),
  );
};

const exportPath = `*/exports/tenants/${northwind.id}/members`;
const fileName = "northwind-members-2026-11-07.csv";

/** The members' CSV file of the web's server, and the query of each request. */
const exportAnswer = (requests: URLSearchParams[] = []) =>
  http.post(exportPath, ({ request }) => {
    requests.push(new URL(request.url).searchParams);
    return HttpResponse.text("\uFEFFName\r\nJohn Byrne\r\n", {
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": `attachment; filename="${fileName}"`,
      },
    });
  });

/** An answer that the server cuts after a page failed: the body errors midway. */
const cutExportAnswer = () =>
  http.post(exportPath, () => {
    const body = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(new TextEncoder().encode("\uFEFFName\r\nJohn"));
        controller.error(new Error("page 2 failed"));
      },
    });
    return new HttpResponse(body, {
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": `attachment; filename="${fileName}"`,
      },
    });
  });

const openExport = async () => {
  await userEvent.click(
    await screen.findByRole("button", { name: "More actions" }),
  );
  await userEvent.click(
    await screen.findByRole("menuitem", { name: "Export CSV" }),
  );
  return screen.findByRole("alertdialog", { name: "Export members" });
};

describe("MembersPage", () => {
  it("MembersPage_Owner_SeesInviteTabsAndEveryAction", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);

    expect(
      await screen.findByRole("button", { name: "Invite member" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("tab", { name: /^Invitations/ }),
    ).toBeInTheDocument();
    await openMenu("John Byrne");
    expect(
      await screen.findByRole("menuitem", { name: "Change role" }),
    ).not.toHaveAttribute("aria-disabled");
    expect(
      screen.getByRole("menuitem", { name: "Remove from organisation" }),
    ).not.toHaveAttribute("aria-disabled");
  });

  // The command menu's "Invite member" (F28): the page opens the dialog once and drops the parameter from the address.
  it("MembersPage_InviteInTheAddress_OpensTheInviteDialogOnce", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />, {
      url: "/northwind/settings/members?invite=1",
    });

    expect(
      await screen.findByRole("alertdialog", { name: "Invite member" }),
    ).toBeInTheDocument();
    await vi.waitFor(() => expect(window.location.search).toBe(""));
  });

  it("MembersPage_YourOwnRow_SaysYouInMutedTextAfterTheName", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);

    const name = await (await table()).findByText("Ayşe Yılmaz");
    const you = within(name.closest("tr") as HTMLElement).getByText("(you)");
    // Frame 8.01 draws it in the status type: 12 px, medium.
    expect(you).toHaveClass("text-muted-foreground", "text-status");
    expect(name.compareDocumentPosition(you)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
  });

  it("MembersPage_NoRoleFilter_NamesAllRoles", async () => {
    server.use(...tenantAnswers(BuiltInRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);

    expect(
      await screen.findByRole("button", { name: "Filter by role: All roles" }),
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
      name: "Dara Quinn",
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
      expect(listRequests(requests).at(-1)?.get("roleId")).toBe(billingLead.id),
    );
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Filter by role: Billing lead" }),
      ).toHaveTextContent(/^Billing lead$/),
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
    await waitFor(() =>
      expect(listRequests(requests).at(-1)?.has("roleId")).toBe(false),
    );
    expect(
      screen.queryByRole("button", { name: "Clear filter" }),
    ).not.toBeInTheDocument();
  });

  // A Member keeps the one Members tab with its count, and no Invitations tab (8.11).
  // A Member changes and removes nobody: only their own row has a menu, which holds Leave (8.18).
  it("MembersPage_Member_SeesAReadOnlyListWithOnlyTheMembersTab", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member), membersAnswer());

    renderInTenant(<MembersPage />);

    expect(await (await table()).findByText("John Byrne")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Invite member" }),
    ).not.toBeInTheDocument();
    expect(
      within(screen.getByRole("tablist"))
        .getAllByRole("tab")
        .map((tab) => tab.textContent),
    ).toEqual(["Members 4"]);
    expect(
      (await table())
        .getAllByRole("button", { name: /Actions for/ })
        .map((button) => button.getAttribute("aria-label")),
    ).toEqual(["Actions for İbrahim Bulut"]);
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

  // 8.29 draws no role filter on the Invitations tab; a role in the address still filters it (F51's "See its invitations").
  it("MembersPage_InvitationsTab_DrawsNoRoleFilterButKeepsTheAddresssRole", async () => {
    const invitationRequests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      invitationsAnswer(invitationRequests, 1),
    );

    renderInTenant(<MembersPage />, {
      url: `/northwind/settings/members?tab=invitations&roleId=${roleOf.member.id}`,
    });

    expect(
      await screen.findByRole("button", { name: "Clear filter" }),
    ).toBeVisible();
    expect(
      screen.queryByRole("button", { name: /^Filter by role/ }),
    ).not.toBeInTheDocument();
    expect(
      invitationRequests.some(
        (params) => params.get("roleId") === roleOf.member.id,
      ),
    ).toBe(true);
  });

  // On a phone the top bar names the page (8.34); the main region keeps the title for screen readers. jsdom draws no breakpoint.
  it("MembersPage_Title_IsForScreenReadersOnlyOnAPhone", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Members" }),
    ).toHaveClass("max-tablet:sr-only");
  });

  it("MembersPage_NoReadPermission_SaysPermissionDenied", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Member, [Permission.TenantProfileRead]),
    );

    renderInTenant(<MembersPage />);

    expect(
      await screen.findByRole("heading", { name: "You can’t open this page" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Back to Home" }),
    ).toHaveAttribute("href", "/northwind");
  });

  it("MembersPage_DeletedUser_SaysDeletedUser", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer([
        ayse,
        {
          ...john,
          name: null,
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

  // The URL holds the filters, the tab and the sort, never the page (F9): an old address with ?page= starts at the first page.
  it("MembersPage_TheUrl_DrivesSearchFilterAndSortButNeverThePage", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(people, requests),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />, {
      url: `/northwind/settings/members?q=byrne&roleId=${roleOf.member.id}&page=2&sort=-name`,
    });

    await waitFor(() => expect(listRequests(requests)).toHaveLength(1));
    expect(
      listRequests(requests).map((params) => Object.fromEntries(params)),
    ).toEqual([
      {
        q: "byrne",
        roleId: roleOf.member.id,
        sort: "-name",
        page: "1",
        pageSize: "50",
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

    await waitFor(() => expect(window.location.search).toBe("?sort=name"));
  });

  it("MembersPage_BackAndForward_RestoreTheTab", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />);
    await userEvent.click(
      await screen.findByRole("tab", { name: /^Invitations/ }),
    );
    await waitFor(() =>
      expect(screen.getByRole("tab", { name: /^Invitations/ })).toHaveAttribute(
        "aria-selected",
        "true",
      ),
    );

    window.history.back();
    await waitFor(() =>
      expect(screen.getByRole("tab", { name: /^Members/ })).toHaveAttribute(
        "aria-selected",
        "true",
      ),
    );
    window.history.forward();
    await waitFor(() =>
      expect(screen.getByRole("tab", { name: /^Invitations/ })).toHaveAttribute(
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
    // No counts while the list cannot load.
    expect(screen.getByRole("tab", { name: "Members" })).toBeInTheDocument();
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

  it("MembersPage_Header_SaysWhoIsInTheOrganisationAndHasNoFreshness", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />);

    expect(
      await screen.findByText(
        "People in Northwind Traders and what they can do.",
      ),
    ).toBeVisible();
    await table();
    expect(screen.queryByText(/^Updated/)).not.toBeInTheDocument();
  });

  it("MembersPage_Columns_ShowTheEmailInItsOwnColumnAndTheRoleAsALabel", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />);

    const rows = within(await screen.findByRole("table", { name: "Members" }));
    expect(
      rows.getAllByRole("columnheader").map((header) => header.textContent),
    ).toEqual(["Name", "Email", "Role", "Joined", "Actions"]);
    const john = (await rows.findByText("John Byrne")).closest(
      "tr",
    ) as HTMLElement;
    expect(within(john).getAllByRole("cell")[1]).toHaveTextContent(
      "john@northwind.io",
    );
    expect(within(john).getAllByRole("cell")[2]).toHaveTextContent(/^Member$/);
  });

  // The address reads left to right inside a cell that keeps the page's direction, so in Arabic it aligns with its header (8.36).
  it("MembersPage_Email_IsALeftToRightTextInACellOfThePagesDirection", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />, { locale: "ar" });

    const email = await within(
      await screen.findByRole("table", { name: "الأعضاء" }),
    ).findByText("john@northwind.io");
    expect(email.tagName).toBe("BDI");
    expect(email).toHaveAttribute("dir", "ltr");
    // The cell's block keeps the page's direction; only the address inside it is left to right.
    expect(email.closest("td")?.firstElementChild).not.toHaveAttribute("dir");
  });

  // The counts are the lists' totals without a filter, so a search does not change them (8.06).
  it("MembersPage_Tabs_CountTheTotalsWithoutTheSearch", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      searchedMembersAnswer(),
      invitationsAnswer([], 2),
    );

    renderInTenant(<MembersPage />, {
      url: "/northwind/settings/members?q=john",
    });

    expect(
      await screen.findByRole("tab", { name: "Members 4" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("tab", { name: "Invitations 2" }),
    ).toBeInTheDocument();
  });

  it("MembersPage_MoreThanOnePage_LoadMoreAddsTheNextPageAndSaysHowMany", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      pagedMembersAnswer(60, requests),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />);
    // Sixty rows of buttons make a role query slow on a busy runner: the table is found once, the rest by text.
    const rows = await table();
    expect(await rows.findByText("Person 50")).toBeInTheDocument();
    expect(rows.queryByText("Person 51")).not.toBeInTheDocument();
    await userEvent.click(screen.getByText("Load more"));

    expect(
      await screen.findByText("10 more members loaded."),
    ).toBeInTheDocument();
    expect(rows.getByText("Person 60")).toBeInTheDocument();
    expect(screen.queryByText("Load more")).not.toBeInTheDocument();
    expect(listRequests(requests).map((params) => params.get("page"))).toEqual([
      "1",
      "2",
    ]);
    expect(window.location.search).toBe("");
  });

  // 8.02: the first load keeps the table's header above its ten rows, so the columns are there before the people.
  it("MembersPage_FirstLoad_ShowsTheColumnsAboveTheRows", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      http.get(`*/v1/tenants/${northwind.id}/members`, async ({ request }) => {
        if (new URL(request.url).searchParams.get("pageSize") !== "1")
          await delay("infinite");
        return HttpResponse.json({ items: [], page: 1, pageSize: 1, total: 4 });
      }),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />);

    const loading = within(
      await screen.findByRole("status", { name: "Loading members" }),
    );
    expect(loading.getByText("Name")).toBeInTheDocument();
    expect(loading.getByText("Email")).toBeInTheDocument();
    expect(loading.getByText("Role")).toBeInTheDocument();
    expect(loading.getByText("Joined")).toBeInTheDocument();
  });

  it("MembersPage_NextPageOnItsWay_ShowsLoadingMoreInPlaceOfTheButton", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      secondPageNeverComesAnswer(),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Load more" }),
    );

    expect(await screen.findByText("Loading more members")).toBeVisible();
    expect(
      screen.queryByRole("button", { name: "Load more" }),
    ).not.toBeInTheDocument();
  });

  it("MembersPage_OnlyYou_InvitesThePeopleYouWorkWith", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer([ayse]),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />);

    expect(await (await table()).findByText("Ayşe Yılmaz")).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "You’re the only member" }),
    ).toBeVisible();
    await userEvent.click(
      screen.getAllByRole("button", { name: "Invite member" })[1] as Element,
    );
    expect(
      await screen.findByRole("alertdialog", { name: "Invite member" }),
    ).toBeInTheDocument();
  });

  it("MembersPage_OnlyYouAndASearch_SaysNothingAboutInviting", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer([ayse]),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />, {
      url: "/northwind/settings/members?q=ayse",
    });

    expect(await (await table()).findByText("Ayşe Yılmaz")).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "You’re the only member" }),
    ).not.toBeInTheDocument();
  });

  it("MembersPage_UnderTheLimit_ShowsNoCountBesideInviteAndNoBanner", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 15 }, { "members.max": 10 }),
      ),
      membersAnswer(),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />);

    await table();
    expect(screen.queryByText(/seats/)).not.toBeInTheDocument();
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("MembersPage_AtTheLimit_InviteOpensNoFreeSeatWithTheSeats", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 6 }, { "members.max": 6 }),
      ),
      membersAnswer(),
      invitationsAnswer([], 2),
    );

    renderInTenant(<MembersPage />);
    await screen.findByRole("tab", { name: "Members 4" });
    const invite = screen.getByRole("button", { name: "Invite member" });
    expect(invite).toBeEnabled();
    await userEvent.click(invite);

    const dialog = await screen.findByRole("alertdialog", {
      name: "No free seat",
    });
    expect(dialog).toHaveTextContent(
      "Northwind Traders uses 6 of 6 seats: 4 members and 2 pending invitations. Remove a member or cancel an invitation to invite someone.",
    );
    expect(dialog).not.toHaveTextContent(/billing|ask us/);
    expect(
      screen.queryByRole("alertdialog", { name: "Invite member" }),
    ).not.toBeInTheDocument();
  });

  it("MembersPage_NoFreeSeat_ViewInvitationsOpensTheInvitationsTab", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 6 }, { "members.max": 6 }),
      ),
      membersAnswer(),
      invitationsAnswer([], 2),
    );

    renderInTenant(<MembersPage />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Invite member" }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "No free seat",
    });
    expect(
      within(dialog).getByRole("button", { name: "Request a plan change" }),
    ).toBeInTheDocument();
    await userEvent.click(
      within(dialog).getByRole("button", { name: "View invitations" }),
    );

    await waitFor(() =>
      expect(window.location.search).toBe("?tab=invitations"),
    );
    expect(
      screen.queryByRole("alertdialog", { name: "No free seat" }),
    ).not.toBeInTheDocument();
  });

  it("MembersPage_NoFreeSeat_RequestAPlanChangeOpensTheSupportContact", async () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 6 }, { "members.max": 6 }),
      ),
      membersAnswer(),
      invitationsAnswer([], 2),
    );

    renderInTenant(<MembersPage />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Invite member" }),
    );
    await userEvent.click(
      within(
        await screen.findByRole("alertdialog", { name: "No free seat" }),
      ).getByRole("button", { name: "Request a plan change" }),
    );

    expect(open).toHaveBeenCalledWith(brand.supportUrl, "_self");
    open.mockRestore();
  });

  it("MembersPage_AtTheLimit_ShowsTheBannerWithNoAction", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 6 }, { "members.max": 6 }),
      ),
      membersAnswer(),
      invitationsAnswer([], 2),
    );

    renderInTenant(<MembersPage />);

    const banner = await screen.findByRole("status");
    expect(banner).toHaveTextContent(
      "Your plan allows 6 seats: members and pending invitations. Remove a member or cancel an invitation to invite someone.",
    );
    expect(within(banner).queryByRole("button")).not.toBeInTheDocument();
    expect(within(banner).queryByRole("link")).not.toBeInTheDocument();
    // The banner sits between the header and the tabs.
    expect(banner.compareDocumentPosition(screen.getByRole("tablist"))).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
    // At the limit, removing stays allowed.
    await openMenu("John Byrne");
    expect(
      await screen.findByRole("menuitem", { name: "Remove from organisation" }),
    ).not.toHaveAttribute("aria-disabled");
  });

  it("MembersPage_OverTheLimitWithPendingInvitations_OffersViewInvitations", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 5 }, { "members.max": 6 }),
      ),
      membersAnswer(),
      invitationsAnswer([], 2),
    );

    renderInTenant(<MembersPage />);

    const banner = await screen.findByRole("status");
    await waitFor(() =>
      expect(banner).toHaveTextContent(
        "Your plan gives 5 seats. You use 6. 4 members and 2 pending invitations. The 2 invitations can still be accepted. You can’t invite anyone new until you’re under 5. Nobody loses access.",
      ),
    );
    expect(banner).not.toHaveTextContent(/after a plan change/);
    expect(within(banner).queryByRole("link")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Invite member" })).toBeEnabled();
    await userEvent.click(
      within(banner).getByRole("button", { name: "View invitations" }),
    );
    await waitFor(() =>
      expect(window.location.search).toBe("?tab=invitations"),
    );
  });

  // The members can outnumber the seats' count for a moment (two reads): the invitations never go below 0, and then nothing to view.
  it("MembersPage_OverTheLimitWithoutPendingInvitations_ShowsNoAction", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 2 }, { "members.max": 3 }),
      ),
      membersAnswer(),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />);

    const banner = await screen.findByRole("status");
    await waitFor(() =>
      expect(banner).toHaveTextContent(
        "4 members and 0 pending invitations. You can’t invite anyone new",
      ),
    );
    expect(within(banner).queryByRole("button")).not.toBeInTheDocument();
  });

  it("MembersPage_OverTheLimitForAViewerWhoCannotReadInvitations_ShowsNoAction", async () => {
    const inviter = customRole(
      "0198f0a2-0000-7000-8000-0000000000c5",
      "Inviter",
      [
        Permission.TenantProfileRead,
        Permission.TenantMembersRead,
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
        planWith({ "members.max": 5 }, { "members.max": 6 }),
      ),
      membersAnswer(),
    );

    renderInTenant(<MembersPage />);

    const banner = await screen.findByRole("status");
    await waitFor(() =>
      expect(banner).toHaveTextContent("4 members and 2 pending invitations."),
    );
    expect(within(banner).queryByRole("button")).not.toBeInTheDocument();
  });

  it("MembersPage_OverTheLimit_NoFreeSeatSaysTheSeatsAndWhatStillWorks", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 5 }, { "members.max": 6 }),
      ),
      membersAnswer(),
      invitationsAnswer([], 2),
    );

    renderInTenant(<MembersPage />);
    await screen.findByRole("tab", { name: "Members 4" });
    await userEvent.click(
      screen.getByRole("button", { name: "Invite member" }),
    );

    expect(
      await screen.findByRole("alertdialog", { name: "No free seat" }),
    ).toHaveTextContent(
      "Your plan gives 5 seats. You use 6: 4 members and 2 pending invitations. The 2 invitations can still be accepted. You can’t invite anyone new until you’re under 5. Nobody loses access.",
    );
  });

  it("MembersPage_OverTheLimitForAMember_ShowsNoBanner", async () => {
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
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("MembersPage_ArabicAtTheLimit_ShowsTheBanner", async () => {
    server.use(
      ...tenantAnswers(
        BuiltInRole.Owner,
        undefined,
        [],
        planWith({ "members.max": 10 }, { "members.max": 10 }),
      ),
      membersAnswer(),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />, { locale: "ar" });

    expect(await screen.findByRole("status")).toHaveTextContent(
      "تسمح خطتك بـ10 من المقاعد: الأعضاء والدعوات المعلّقة. أزل عضوًا أو ألغِ دعوة لدعوة شخص.",
    );
  });

  // 8.41, 8.08, 8.12: the file of the list as it is, under the server's name, then a toast that names it.
  it("MembersPage_Export_SavesTheFileUnderTheServersNameAndSaysSo", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      invitationsAnswer(),
      exportAnswer(requests),
    );
    const saved = savedFiles();

    renderInTenant(<MembersPage />);
    await table();
    const dialog = await openExport();
    expect(dialog).toHaveTextContent(
      "A CSV file with 4 members of Northwind Traders: name, email, role, joined date. Pending invitations are not in it.",
    );
    expect(dialog).toHaveTextContent(
      "Filter: All roles. The file follows the list as you see it.",
    );
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Export CSV" }),
    );

    await expectVisibleToast("Members exported");
    expect(
      screen.getByText(`${fileName} is in your downloads.`),
    ).toBeInTheDocument();
    expect(saved).toEqual([fileName]);
    expect(
      screen.queryByRole("alertdialog", { name: "Export members" }),
    ).not.toBeInTheDocument();
    expect(Object.fromEntries(requests[0] as URLSearchParams)).toEqual({
      timeZone: browserTimeZone(),
    });
  });

  // 8.10: the count and the role of the filter, which the file follows.
  it("MembersPage_ExportFilteredByRole_SaysTheCountAndTheRoleAndSendsThem", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer([mehmet]),
      invitationsAnswer(),
      exportAnswer(requests),
    );
    savedFiles();

    renderInTenant(<MembersPage />, {
      url: `/northwind/settings/members?roleId=${roleOf.admin.id}&sort=name`,
    });
    await table();
    const dialog = await openExport();
    expect(dialog).toHaveTextContent(
      "A CSV file with 1 member: name, email, role, joined date.",
    );
    expect(dialog).toHaveTextContent(
      "Filter: Admin. Clear the filter to export everyone.",
    );
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Export CSV" }),
    );

    await expectVisibleToast("Members exported");
    expect(Object.fromEntries(requests[0] as URLSearchParams)).toEqual({
      roleId: roleOf.admin.id,
      sort: "name",
      timeZone: browserTimeZone(),
    });
  });

  it("MembersPage_ExportASearch_SaysTheSearch", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer([john]),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />, {
      url: "/northwind/settings/members?q=john",
    });
    await table();
    const dialog = await openExport();

    expect(dialog).toHaveTextContent(
      "Search: “john”. Clear the search to export everyone.",
    );
  });

  it("MembersPage_ExportASearchAndARole_SaysBoth", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer([john]),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />, {
      url: `/northwind/settings/members?q=john&roleId=${roleOf.member.id}`,
    });
    await table();
    const dialog = await openExport();

    expect(dialog).toHaveTextContent(
      "Search: “john”. Filter: Member. Clear the search and the filter to export everyone.",
    );
  });

  // 8.16 with F4: a refusal shows its own text and reference; the dialog stays open to try again.
  it("MembersPage_ExportRefused_ShowsTheProblemAndStaysOpen", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      invitationsAnswer(),
      http.post(exportPath, () => problem(403, ErrorCode.AuthForbidden)),
    );
    const saved = savedFiles();

    renderInTenant(<MembersPage />);
    await table();
    const dialog = await openExport();
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Export CSV" }),
    );

    expect(
      await within(dialog).findByText(/^Couldn’t create the file\./),
    ).toHaveTextContent(
      /You do not have permission to do this\. Ref 4bf92f3577b34da6a3ce929d0e0e4736/,
    );
    expect(
      within(dialog).getByRole("button", { name: "Try again" }),
    ).toBeInTheDocument();
    expect(saved).toEqual([]);
  });

  // Half a file is never a success (the maintainer's decision of 2026-11-07): a cut answer saves nothing and says so.
  it("MembersPage_ExportAnswerCut_ShowsTheErrorAndSavesNoFile", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(),
      invitationsAnswer(),
      cutExportAnswer(),
    );
    const saved = savedFiles();

    renderInTenant(<MembersPage />);
    await table();
    const dialog = await openExport();
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Export CSV" }),
    );

    expect(
      await within(dialog).findByText(/^Couldn’t create the file\./),
    ).toHaveTextContent("Something went wrong on our side.");
    expect(saved).toEqual([]);
    expect(screen.queryByText("Members exported")).not.toBeInTheDocument();
  });

  // 8.14: with nobody in the list, Export stays and says why (F51's pattern).
  it("MembersPage_SearchFindsNothing_DisablesExportAndSaysWhy", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer([]),
      invitationsAnswer(),
    );

    renderInTenant(<MembersPage />, {
      url: "/northwind/settings/members?q=zzq",
    });
    await screen.findByRole("heading", { name: "No members match “zzq”" });
    await userEvent.click(screen.getByRole("button", { name: "More actions" }));

    const item = await screen.findByRole("menuitem", { name: /Export CSV/ });
    expect(item).toHaveAttribute("aria-disabled", "true");
    expect(item).toHaveTextContent("No member matches the search.");
  });

  // 8.11: whoever reads the members may export them (ExportMembers needs tenant.members.read).
  it("MembersPage_Member_CanExport", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member), membersAnswer());

    renderInTenant(<MembersPage />);
    await table();
    await userEvent.click(screen.getByRole("button", { name: "More actions" }));

    expect(
      await screen.findByRole("menuitem", { name: "Export CSV" }),
    ).not.toHaveAttribute("aria-disabled");
  });
});
