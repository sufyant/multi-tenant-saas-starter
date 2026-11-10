import { ErrorCode, UserResponseStatus } from "@repo/api-client";
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
import {
  atlas,
  meAnswer,
  northwind,
  problem,
} from "../../../test/organisations";
import { sentence } from "../../../test/render";
import { openUserAction } from "../../../test/user-page";
import {
  ayse,
  ciara,
  membershipOf,
  platformCommand,
  userAnswer,
  userAnswers,
  userDetailOf,
} from "../../../test/users";

const router = vi.hoisted(() => ({ push: vi.fn(), refresh: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

// Ayşe is an Admin of Atlas Logistics only, so nothing stops her deletion before the click.
const detail = userDetailOf(ayse, {
  memberships: [membershipOf(atlas)],
  membershipCount: 1,
});

const open = (user = ayse, name = "Ayşe Yılmaz") =>
  openUserAction(user, name, "Delete user", `Delete ${name}?`);

const confirm = async (
  dialog: Awaited<ReturnType<typeof open>>,
  email = "ayse@northwind.example",
) => {
  await userEvent.type(
    dialog.getByRole("textbox", { name: `Type ${email} to confirm` }),
    email,
  );
  await userEvent.click(dialog.getByRole("button", { name: "Delete user" }));
};

describe("DeleteUserDialog", () => {
  it("Delete_TheEmailNotTyped_KeepsTheButtonDisabled", async () => {
    server.use(meAnswer(), userAnswer(detail));

    const dialog = await open();

    expect(
      dialog.getByText(
        sentence(
          "Ayşe Yılmaz’s account is deleted, and they leave Atlas Logistics. The Activity log keeps their entries as “Deleted user”. This can’t be undone.",
        ),
      ),
    ).toBeVisible();
    expect(dialog.getByRole("button", { name: "Delete user" })).toBeDisabled();
  });

  it("Delete_Success_ToastsAndThePageShowsTheDeletedState", async () => {
    const reads = { count: 0 };
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswers(
        [
          detail,
          userDetailOf(ayse, {
            name: null,
            email: null,
            status: UserResponseStatus.Deleted,
            membershipCount: 0,
          }),
        ],
        reads,
      ),
      platformCommand(
        "delete",
        `users/${ayse.id}`,
        [() => new HttpResponse(null, { status: 204 })],
        calls,
      ),
    );

    const dialog = await open();
    await confirm(dialog);

    await expectVisibleToast(sentence("Ayşe Yılmaz deleted."));
    expect(calls).toEqual([{ key: null, body: null }]);
    expect(
      await screen.findByRole("heading", { level: 1, name: "Deleted user" }),
    ).toBeVisible();
  });

  it("Delete_NoMemberships_SaysOnlyThatTheAccountIsDeleted", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { memberships: [], membershipCount: 0 })),
    );

    const dialog = await open();

    expect(
      dialog.getByText(
        sentence(
          "Ayşe Yılmaz’s account is deleted. The Activity log keeps their entries as “Deleted user”. This can’t be undone.",
        ),
      ),
    ).toBeVisible();
  });

  it("Delete_SystemAdmin_OpensWithTheReasonAndSendsNothing", async () => {
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ciara)),
      platformCommand("delete", `users/${ciara.id}`, [], calls),
    );

    const dialog = await openUserAction(
      ciara,
      "Ciara Byrne",
      "Delete user",
      "Ciara Byrne can’t be deleted",
    );

    expect(
      dialog.getByText(
        sentence(
          "Ciara Byrne is a system admin. Remove the system admin access in Settings › System admins first.",
        ),
      ),
    ).toBeVisible();
    expect(dialog.queryByRole("textbox")).toBeNull();
    await userEvent.click(dialog.getByRole("button", { name: "Close" }));
    expect(screen.queryByRole("alertdialog")).toBeNull();
    expect(calls).toHaveLength(0);
  });

  it("Delete_LastOwnerOfTwo_OpensWithTheReasonNamingBoth", async () => {
    server.use(
      meAnswer(),
      userAnswer(
        userDetailOf(ayse, {
          memberships: [
            membershipOf(northwind, { lastOwner: true }),
            membershipOf(atlas, { lastOwner: true }),
          ],
        }),
      ),
    );

    const dialog = await openUserAction(
      ayse,
      "Ayşe Yılmaz",
      "Delete user",
      "Ayşe Yılmaz can’t be deleted yet",
    );

    expect(
      dialog.getByText(
        sentence(
          "Ayşe Yılmaz is the only Owner of Northwind Traders and Atlas Logistics. Make someone else Owner there first.",
        ),
      ),
    ).toBeVisible();
    expect(dialog.getByRole("button", { name: "Close" })).toBeVisible();
  });

  // The API stays the guard: a user whom another admin made a system admin after the page read GetUser.
  it("Delete_SystemAdminMeanwhile_SaysWhyAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      userAnswer(detail, reads),
      platformCommand("delete", `users/${ayse.id}`, [
        () => problem(409, ErrorCode.UserIsSystemAdmin),
      ]),
    );

    const dialog = await open();
    await confirm(dialog);

    expect(
      await screen.findByRole("alertdialog", {
        name: "Ayşe Yılmaz can’t be deleted",
      }),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });

  // The API stays the guard: an Owner who became the last one after the page read GetUser.
  it("Delete_LastOwnerMeanwhile_SaysWhyAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      userAnswers(
        [
          detail,
          userDetailOf(ayse, {
            memberships: [membershipOf(atlas, { lastOwner: true })],
          }),
        ],
        reads,
      ),
      platformCommand("delete", `users/${ayse.id}`, [
        () => problem(409, ErrorCode.UserLastOwner, undefined, [atlas.id]),
      ]),
    );

    const dialog = await open();
    await confirm(dialog);

    const refused = await screen.findByRole("alertdialog", {
      name: "Ayşe Yılmaz can’t be deleted yet",
    });
    expect(refused).toHaveTextContent(
      "Ayşe Yılmaz is the only Owner of Atlas Logistics. Make someone else Owner there first.",
    );
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Delete_JoinedMeanwhile_SaysNothingWasDeletedAndReadsAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      userAnswer(detail, reads),
      platformCommand("delete", `users/${ayse.id}`, [
        () => problem(409, ErrorCode.UserHasMemberships),
      ]),
    );

    const dialog = await open();
    await confirm(dialog);

    expect(
      await dialog.findByText(
        sentence("Ayşe Yılmaz joined an organisation while this ran."),
      ),
    ).toBeVisible();
    expect(dialog.getByText("Nothing was deleted. Try again.")).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });
});
