import { ErrorCode, UserResponseStatus } from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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
import { meAnswer, northwind, problem } from "../../../test/organisations";
import { sentence } from "../../../test/render";
import { openUserAction } from "../../../test/user-page";
import {
  john,
  membershipOf,
  platformCommand,
  userAnswer,
  userDetailOf,
  userResponse,
} from "../../../test/users";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const suspended = userDetailOf(john, {
  suspendedAt: "2026-10-16T09:00:00Z",
  suspendedBy: { name: "Ciara Byrne", deleted: false },
  memberships: [membershipOf(northwind)],
});

const open = () =>
  openUserAction(john, "John Byrne", "Reactivate", "Reactivate John Byrne?");

const reactivate = (dialog: Awaited<ReturnType<typeof open>>) =>
  userEvent.click(dialog.getByRole("button", { name: "Reactivate" }));

describe("ReactivateUserDialog", () => {
  it("Reactivate_Success_ToastsAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(suspended, reads),
      platformCommand(
        "post",
        `users/${john.id}/reactivate`,
        [userResponse(john, { status: UserResponseStatus.Active })],
        calls,
      ),
    );

    const dialog = await open();
    expect(
      dialog.getByText(
        sentence(
          "John Byrne can sign in again at once, with the same roles in Northwind Traders.",
        ),
      ),
    ).toBeVisible();
    await reactivate(dialog);

    await expectVisibleToast(sentence("John Byrne reactivated."));
    expect(calls).toEqual([{ key: null, body: null }]);
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Reactivate_OrganisationEndingInAStop_EndsTheSentenceWithOneStop", async () => {
    server.use(
      meAnswer(),
      userAnswer({
        ...suspended,
        memberships: [
          membershipOf({ ...northwind, name: "Pacific Coast Freight Ltd." }),
        ],
      }),
    );

    const dialog = await open();

    expect(
      dialog.getByText(
        sentence(
          "John Byrne can sign in again at once, with the same roles in Pacific Coast Freight Ltd.",
        ),
      ),
    ).toBeVisible();
  });

  it("Reactivate_NotFound_SaysSoAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      userAnswer(suspended, reads),
      platformCommand("post", `users/${john.id}/reactivate`, [
        () => problem(404, ErrorCode.UserNotFound),
      ]),
    );

    const dialog = await open();
    await reactivate(dialog);

    expect(
      await dialog.findByText("This user no longer exists."),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });
});
