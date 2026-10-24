import { ErrorCode, UserResponseStatus } from "@repo/api-client";
import { screen, waitFor } from "@testing-library/react";
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
import { meAnswer, problem } from "../../../test/organisations";
import { openUserAction } from "../../../test/user-page";
import {
  john,
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
  suspendedBy: { firstName: "Ciara", lastName: "Byrne", deleted: false },
});

const open = () =>
  openUserAction(john, "John Byrne", "Reactivate", "Reactivate John Byrne?");

const reactivate = (dialog: Awaited<ReturnType<typeof open>>) =>
  userEvent.click(dialog.getByRole("button", { name: "Reactivate user" }));

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
        "They can sign in again at once, in every organisation they belong to.",
      ),
    ).toBeVisible();
    await reactivate(dialog);

    expect(await screen.findByText("John Byrne reactivated.")).toBeVisible();
    expect(calls).toEqual([{ key: null, body: null }]);
    await waitFor(() => expect(reads.count).toBe(2));
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
