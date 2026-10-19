import { ErrorCode, MemberRole } from "@repo/api-client";
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
  pageTestTimeout,
  people,
  permissionsOf,
  pickFromRowMenu,
  problem,
  renderInTenant,
  tenantAnswers,
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

// Each test renders the whole page and drives it like a person: on CI, beside the builds, one takes up to 8 times as long
// as here, and the first of a file also pays the warm-up. The test waits longer; it never retries.
describe("ChangeRoleDialog", { timeout: pageTestTimeout }, () => {
  it("ChangeRole_Success_ClosesToastsAndRefreshesTheList", async () => {
    const requests: URLSearchParams[] = [];
    const bodies: unknown[] = [];
    server.use(
      ...tenantAnswers(MemberRole.Owner),
      membersAnswer(people, requests),
      changeAnswer(
        () =>
          HttpResponse.json({
            ...john,
            role: { builtIn: MemberRole.Admin, name: MemberRole.Admin },
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
    expect(bodies).toEqual([{ role: MemberRole.Admin }]);
    expect(requests).toHaveLength(2);
    await waitFor(() =>
      expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument(),
    );
  });

  it("ChangeRole_Owner_OffersTheThreeRolesWithTheirDescriptions", async () => {
    server.use(...tenantAnswers(MemberRole.Owner), membersAnswer());

    renderInTenant(<MembersPage />);
    const dialog = await openChangeRole();

    expect(within(dialog).getAllByRole("radio")).toHaveLength(3);
    expect(dialog).toHaveTextContent(
      "Everything an Admin can do, and can give or take the Owner role.",
    );
    expect(within(dialog).getByRole("radio", { name: "Member" })).toBeChecked();
  });

  it("ChangeRole_Admin_OffersNoOwner", async () => {
    server.use(...tenantAnswers(MemberRole.Admin), membersAnswer());

    renderInTenant(<MembersPage />);
    const dialog = await openChangeRole();

    expect(
      within(dialog).queryByRole("radio", { name: "Owner" }),
    ).not.toBeInTheDocument();
    expect(within(dialog).getAllByRole("radio")).toHaveLength(2);
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
  ])(
    "ChangeRole_Refused%s_%s_ShowsTheBoardsTextInTheDialog",
    async (status, code, title, text) => {
      server.use(
        ...tenantAnswers(MemberRole.Owner),
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
      ...tenantAnswers(MemberRole.Owner),
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

  it("ChangeRole_YourOwnRole_RefreshesYourPermissions", async () => {
    const reads: string[] = [];
    server.use(
      countedTenant(reads),
      ...tenantAnswers(MemberRole.Owner),
      membersAnswer(),
      http.patch(`*/v1/tenants/${northwind.id}/members/${ayse.userId}`, () =>
        HttpResponse.json({
          ...ayse,
          role: { builtIn: MemberRole.Admin, name: MemberRole.Admin },
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
        ...tenantAnswers(MemberRole.Owner),
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
