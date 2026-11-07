import { BuiltInRole, ErrorCode, type MemberResponse } from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
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
  northwind,
  people,
  pickFromRowMenu,
  problem,
  renderInTenant,
  roleOf,
  tenantAnswers,
} from "../../../test/tenant";
import { MembersPage } from "./members-page";

const router = vi.hoisted(() => ({ replace: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));
vi.mock("@clerk/nextjs", () => ({ useClerk: () => ({ signOut: vi.fn() }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  router.replace.mockReset();
});
afterAll(() => server.close());

/**
 * ListMembers as the API answers it: the people, and for the filter of the Owner role only as many Owners as `owners` says, the
 * read that tells the web, before the click, whether the viewer is the last Owner (8.46).
 */
const membersWith = (owners: number, items: MemberResponse[] = people) =>
  http.get(`*/v1/tenants/${northwind.id}/members`, ({ request }) => {
    const params = new URL(request.url).searchParams;
    const total =
      params.get("roleId") === roleOf.owner.id ? owners : items.length;
    return HttpResponse.json({ items, page: 1, pageSize: 20, total });
  });

/** ListInvitations without a pending one: the Invitations tab's count of an Owner. */
const noInvitations = http.get(`*/v1/tenants/${northwind.id}/invitations`, () =>
  HttpResponse.json({ items: [], page: 1, pageSize: 20, total: 0 }),
);

const leaveAnswer = (answer: () => Response, calls = { count: 0 }) =>
  http.delete(`*/v1/tenants/${northwind.id}/membership`, () => {
    calls.count += 1;
    return answer();
  });

const openLeave = async (name: string) => {
  await pickFromRowMenu("Members", name, "Leave organisation");
  return screen.findByRole("alertdialog", {
    name: "Leave Northwind Traders?",
  });
};

const ownRowMenu = async (name: string) => {
  const table = within(await screen.findByRole("table", { name: "Members" }));
  await userEvent.click(
    await table.findByRole("button", { name: `Actions for ${name}` }),
  );
  return screen.findByRole("menu");
};

describe("LeaveDialog", () => {
  // 8.18: the person's own row offers Leave in place of Remove; nobody removes themselves.
  it("Leave_TheOwnRowOfAnOwner_OffersChangeRoleAndLeave", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      noInvitations,
      membersWith(2),
    );

    renderInTenant(<MembersPage />);
    const menu = await ownRowMenu("Ayşe Yılmaz");

    expect(
      within(menu)
        .getAllByRole("menuitem")
        .map((item) => item.firstChild?.textContent),
    ).toEqual(["Change role", "Leave organisation"]);
  });

  // A Member may change and remove nobody, so only their own row has a menu, with Leave alone.
  it("Leave_AMember_HasAMenuOnTheirOwnRowOnly", async () => {
    server.use(...tenantAnswers(BuiltInRole.Member), membersWith(1));

    renderInTenant(<MembersPage />);
    const table = within(await screen.findByRole("table", { name: "Members" }));

    expect(
      await table.findByRole("button", { name: "Actions for İbrahim Bulut" }),
    ).toBeInTheDocument();
    expect(
      table.queryByRole("button", { name: "Actions for John Byrne" }),
    ).not.toBeInTheDocument();
  });

  // 8.23 and 8.24: the dialog names the organisation; after the answer the toast says what stays and the start page opens.
  it("Leave_Confirmed_LeavesToastsAndOpensTheStartPage", async () => {
    const calls = { count: 0 };
    server.use(
      ...tenantAnswers(BuiltInRole.Member),
      membersWith(1),
      leaveAnswer(() => new HttpResponse(null, { status: 204 }), calls),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openLeave("İbrahim Bulut");
    expect(dialog).toHaveTextContent(
      "You lose access at once. Your past activity stays in the log. To come back, ask an Owner or Admin to invite you again.",
    );
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Leave organisation" }),
    );

    await expectVisibleToast("You left Northwind Traders");
    expect(
      screen.getByText("Your past activity stays in its log."),
    ).toBeInTheDocument();
    expect(calls.count).toBe(1);
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/"));
  });

  // 8.46: the web knows the last Owner before the click, from ListMembers filtered by the Owner role.
  it("Leave_TheLastOwner_IsDisabledAndSaysWhy", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      noInvitations,
      membersWith(1),
    );

    renderInTenant(<MembersPage />);
    const menu = await ownRowMenu("Ayşe Yılmaz");

    const leave = await within(menu).findByRole("menuitem", {
      name: /Leave organisation/,
    });
    await waitFor(() => expect(leave).toHaveAttribute("aria-disabled", "true"));
    expect(leave).toHaveTextContent(
      "You are the last Owner. Make another member an Owner first, or contact support to close the organisation.",
    );
  });

  // 8.25: 409 `member.last_owner` stays the fallback, when another Owner left after the list was read.
  it("Leave_RefusedLastOwner_SaysSoWithContactSupportAndCloses", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      noInvitations,
      membersWith(2),
      leaveAnswer(() => problem(409, ErrorCode.MemberLastOwner)),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openLeave("Ayşe Yılmaz");
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Leave organisation" }),
    );

    const refused = await screen.findByRole("alertdialog", {
      name: "Leave Northwind Traders?",
    });
    const banner = await within(refused).findByRole("status");
    expect(banner).toHaveTextContent(
      "Northwind Traders needs at least one Owner.",
    );
    expect(banner).toHaveTextContent(
      "Make another member an Owner first, or contact support to close the organisation.",
    );
    expect(
      within(banner).getByRole("link", { name: "contact support" }),
    ).toHaveAttribute("href", "mailto:support@starter.example");
    expect(
      within(refused).getByRole("button", { name: "Close" }),
    ).toBeInTheDocument();
    expect(router.replace).not.toHaveBeenCalled();
  });

  it("Leave_RefusedOtherwise_SaysWhyInTheDialog", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Member),
      membersWith(1),
      leaveAnswer(() => problem(403, ErrorCode.TenantSuspended)),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openLeave("İbrahim Bulut");
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Leave organisation" }),
    );

    const banner = await within(dialog).findByRole("status");
    expect(banner).toHaveTextContent("Couldn’t leave Northwind Traders");
    expect(router.replace).not.toHaveBeenCalled();
  });
});
