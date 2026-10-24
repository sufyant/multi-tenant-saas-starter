import { ErrorCode } from "@repo/api-client";
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
  john,
  membersAnswer,
  northwind,
  people,
  pickFromRowMenu,
  problem,
  renderInTenant,
  tenantAnswers,
} from "../../../test/tenant";
import { MembersPage } from "./members-page";

vi.mock("@clerk/nextjs", () => ({ useClerk: () => ({ signOut: vi.fn() }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const removeAnswer = (answer: () => Response) =>
  http.delete(`*/v1/tenants/${northwind.id}/members/${john.userId}`, answer);

const openRemove = async () => {
  await pickFromRowMenu("Members", "John Byrne", "Remove from organisation");
  return screen.findByRole("alertdialog", {
    name: "Remove John Byrne from Northwind Traders?",
  });
};

describe("RemoveMemberDialog", () => {
  it("RemoveMember_Success_ClosesToastsAndRefreshesTheList", async () => {
    const requests: URLSearchParams[] = [];
    server.use(
      ...tenantAnswers(BuiltInRole.Owner),
      membersAnswer(people, requests),
      removeAnswer(() => new HttpResponse(null, { status: 204 })),
    );

    renderInTenant(<MembersPage />);
    const dialog = await openRemove();
    expect(dialog).toHaveTextContent(
      "They lose access at once. Their past activity stays in the log.",
    );
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Remove member" }),
    );

    expect(
      await screen.findByText("John Byrne removed from Northwind Traders"),
    ).toBeInTheDocument();
    expect(requests).toHaveLength(2);
    await waitFor(() =>
      expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument(),
    );
  });

  it.each([
    [
      409,
      ErrorCode.MemberLastOwner,
      "Northwind Traders needs at least one Owner.",
      "Make someone else an Owner first, then remove this member.",
    ],
    [
      403,
      ErrorCode.MemberOwnerRequiresOwner,
      "Only an Owner can remove an Owner.",
      "Ask an Owner of Northwind Traders.",
    ],
    [
      404,
      ErrorCode.MemberNotFound,
      "John Byrne is no longer a member.",
      "Someone removed them already. The list has been refreshed.",
    ],
    [
      403,
      ErrorCode.MemberCannotRemoveSelf,
      "Couldn’t remove John Byrne",
      "You cannot remove yourself.",
    ],
    [
      403,
      ErrorCode.RoleEscalation,
      "This role has permissions your role doesn’t have.",
      "Ask an Owner of Northwind Traders to do this.",
    ],
  ])(
    "RemoveMember_Refused%s_%s_ShowsTheBoardsTextInTheDialog",
    async (status, code, title, text) => {
      server.use(
        ...tenantAnswers(BuiltInRole.Owner),
        membersAnswer(),
        removeAnswer(() => problem(status, code)),
      );

      renderInTenant(<MembersPage />);
      const dialog = await openRemove();
      await userEvent.click(
        within(dialog).getByRole("button", { name: "Remove member" }),
      );

      const banner = await within(dialog).findByRole("status");
      expect(banner).toHaveTextContent(title);
      expect(banner).toHaveTextContent(text);
    },
  );
});
