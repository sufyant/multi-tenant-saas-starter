import { ErrorCode } from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse } from "msw";
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
import { openMakeOwner } from "../../../test/user-page";
import {
  mehmet,
  mehmetMemberships,
  platformCommand,
  userAnswer,
  userDetailOf,
} from "../../../test/users";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const title = "Make Mehmet Kaya an Owner of Northwind Traders?";
const open = () => openMakeOwner(mehmet, "Northwind Traders", title);
const detail = userDetailOf(mehmet, { memberships: mehmetMemberships });

const makeOwner = async (dialog: Awaited<ReturnType<typeof open>>) =>
  userEvent.click(dialog.getByRole("button", { name: "Make Owner" }));

describe("MakeOwnerDialog", () => {
  it("MakeOwner_Success_SendsTheUserWithoutAKeyAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(detail, reads),
      platformCommand(
        "post",
        `tenants/${northwind.id}/owners`,
        [() => HttpResponse.json(mehmetMemberships[0])],
        calls,
      ),
    );

    const dialog = await open();
    expect(
      dialog.getByText(
        sentence(
          "They get every permission in Northwind Traders. The organisation’s Activity shows that the platform did this.",
        ),
      ),
    ).toBeVisible();
    await makeOwner(dialog);

    await expectVisibleToast(
      sentence("Mehmet Kaya is an Owner of Northwind Traders."),
    );
    expect(calls).toEqual([{ key: null, body: { userId: mehmet.id } }]);
    await waitFor(() => expect(reads.count).toBe(2));
    expect(screen.queryByRole("alertdialog")).toBeNull();
  });

  it.each([
    [
      409,
      ErrorCode.MemberAlreadyOwner,
      "Mehmet Kaya is already an Owner of Northwind Traders. The page has been refreshed.",
    ],
    [
      409,
      ErrorCode.TenantNotActive,
      "Northwind Traders is not active. Reactivate or reopen it first.",
    ],
    [
      404,
      ErrorCode.MemberNotFound,
      "Mehmet Kaya is no longer a member of Northwind Traders.",
    ],
    [409, ErrorCode.UserSuspended, "Reactivate Mehmet Kaya first."],
    [409, ErrorCode.UserDeleted, "This user was deleted."],
  ])(
    "MakeOwner_%s%s_ShowsItsTextAndReadsTheUserAgain",
    async (status, code, text) => {
      const reads = { count: 0 };
      server.use(
        meAnswer(),
        userAnswer(detail, reads),
        platformCommand("post", `tenants/${northwind.id}/owners`, [
          () => problem(status, code),
        ]),
      );

      const dialog = await open();
      await makeOwner(dialog);

      expect(await dialog.findByText(sentence(text))).toBeVisible();
      await waitFor(() => expect(reads.count).toBe(2));
    },
  );

  it("MakeOwner_ServerError_SaysItWasNotDone", async () => {
    server.use(
      meAnswer(),
      userAnswer(detail),
      platformCommand("post", `tenants/${northwind.id}/owners`, [
        () => problem(500, ErrorCode.ServerError),
      ]),
    );

    const dialog = await open();
    await makeOwner(dialog);

    expect(
      await dialog.findByText(sentence("Mehmet Kaya did not change.")),
    ).toBeVisible();
  });
});
