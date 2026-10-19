import { MemberRole, Permission } from "@repo/api-client";
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
  john,
  membersAnswer,
  northwind,
  people,
  problem,
  renderInTenant,
  tenantAnswers,
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
    server.use(...tenantAnswers(MemberRole.Owner), membersAnswer());

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
    server.use(...tenantAnswers(MemberRole.Owner), membersAnswer());

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

  it("MembersPage_YourOwnRow_HasTheYouTag", async () => {
    server.use(...tenantAnswers(MemberRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);

    const row = (await (await table()).findByText("Ayşe Yılmaz")).closest("tr");
    expect(row).toHaveTextContent("You");
  });

  it("MembersPage_Admin_CannotTouchAnOwnerAndSaysWhy", async () => {
    server.use(...tenantAnswers(MemberRole.Admin), membersAnswer());

    renderInTenant(<MembersPage />);
    await openMenu("Ayşe Yılmaz");

    const change = await screen.findByRole("menuitem", { name: /Change role/ });
    expect(change).toHaveAttribute("aria-disabled", "true");
    expect(change).toHaveTextContent("Only an Owner can change an Owner.");
    expect(
      screen.getByRole("menuitem", { name: /Remove from organisation/ }),
    ).toHaveTextContent("Only an Owner can remove an Owner.");
  });

  it("MembersPage_Member_SeesAReadOnlyListWithoutTabsOrInvite", async () => {
    server.use(...tenantAnswers(MemberRole.Member), membersAnswer());

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
      ...tenantAnswers(MemberRole.Member),
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
      ...tenantAnswers(MemberRole.Member, [Permission.TenantProfileRead]),
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
      ...tenantAnswers(MemberRole.Owner),
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
      ...tenantAnswers(MemberRole.Owner),
      membersAnswer(people, requests),
    );

    renderInTenant(<MembersPage />, {
      url: "/northwind/settings/members?q=byrne&role=member&page=2&sort=-lastName",
    });

    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests.map((params) => Object.fromEntries(params))).toEqual([
      {
        q: "byrne",
        role: MemberRole.Member,
        page: "2",
        sort: "-lastName",
      },
    ]);
    expect(
      screen.getByRole("searchbox", { name: "Search by name or email" }),
    ).toHaveValue("byrne");
  });

  it("MembersPage_TypeASearch_WritesItToTheUrlAfterAPause", async () => {
    server.use(...tenantAnswers(MemberRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);
    await userEvent.type(
      await screen.findByRole("searchbox", { name: "Search by name or email" }),
      "john",
    );

    expect(window.location.search).toBe("");
    await waitFor(() => expect(window.location.search).toBe("?q=john"));
  });

  it("MembersPage_SortByName_WritesTheSortToTheUrl", async () => {
    server.use(...tenantAnswers(MemberRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);
    await userEvent.click(
      await (await table()).findByRole("button", { name: "Name" }),
    );

    await waitFor(() => expect(window.location.search).toBe("?sort=lastName"));
  });

  it("MembersPage_BackAndForward_RestoreTheTab", async () => {
    server.use(
      ...tenantAnswers(MemberRole.Owner),
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
    server.use(...tenantAnswers(MemberRole.Owner), membersAnswer([]));

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
      ...tenantAnswers(MemberRole.Owner),
      http.get(`*/v1/tenants/${northwind.id}/members`, () =>
        problem(500, "server.error"),
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
});
