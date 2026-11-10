import {
  ErrorCode,
  FieldErrorCode,
  UserResponseStatus,
} from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, waitFor, within } from "@testing-library/react";
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
import {
  atlas,
  kestrel,
  meAnswer,
  northwind,
  problem,
  shannon,
} from "../../../test/organisations";
import { renderWithProviders, sentence } from "../../../test/render";
import { openUserAction } from "../../../test/user-page";
import {
  ayse,
  ayseMemberships,
  ciara,
  mehmet,
  mehmetMemberships,
  membershipOf,
  platformCommand,
  unnamed,
  userAnswer,
  userDetailOf,
  userResponse,
} from "../../../test/users";
import { UserPage } from "./user-page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const open = (user = ayse, name = "Ayşe Yılmaz") =>
  openUserAction(user, name, "Suspend", `Suspend ${name}?`);

const suspend = async (
  dialog: Awaited<ReturnType<typeof open>>,
  reason = "Shared account",
) => {
  await userEvent.type(dialog.getByRole("textbox", { name: "Reason" }), reason);
  await userEvent.click(dialog.getByRole("button", { name: "Suspend" }));
};

const lastOwnerOf = (
  ...organisations: { id: string; name: string; slug: string }[]
) =>
  organisations.map((organisation) =>
    membershipOf(organisation, { lastOwner: true }),
  );

describe("SuspendUserDialog", () => {
  it("Suspend_Success_ToastsAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(
        userDetailOf(mehmet, { memberships: mehmetMemberships }),
        reads,
      ),
      platformCommand(
        "post",
        `users/${mehmet.id}/suspend`,
        [userResponse(mehmet, { status: UserResponseStatus.Suspended })],
        calls,
      ),
    );

    const dialog = await open(mehmet, "Mehmet Kaya");
    expect(
      dialog.getByText(
        sentence(
          "Mehmet Kaya is signed out everywhere and can’t sign in until you reactivate them. Nothing is deleted.",
        ),
      ),
    ).toBeVisible();
    expect(
      dialog.getByText("Required. Only the platform sees it."),
    ).toBeVisible();
    expect(dialog.queryByText(/is the only Owner of/)).toBeNull();
    await suspend(dialog);

    await expectVisibleToast(sentence("Mehmet Kaya suspended"));
    expect(
      screen.getByText("They are refused on their next request."),
    ).toBeVisible();
    expect(calls).toEqual([{ key: null, body: { reason: "Shared account" } }]);
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Suspend_LastOwner_WarnsByTheOrganisationsNameAndStillSuspends", async () => {
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse, { memberships: ayseMemberships })),
      platformCommand(
        "post",
        `users/${ayse.id}/suspend`,
        [userResponse(ayse, { status: UserResponseStatus.Suspended })],
        calls,
      ),
    );

    const dialog = await open();
    expect(
      dialog.getByText(
        sentence(
          "Ayşe Yılmaz is the only Owner of Northwind Traders. While they are suspended, nobody there can manage Owners. Make someone else Owner first if Northwind Traders needs one.",
        ),
      ),
    ).toHaveClass("text-destructive-text");
    expect(dialog.getByRole("button", { name: "Suspend" })).toBeEnabled();
    await suspend(dialog);

    await expectVisibleToast(sentence("Ayşe Yılmaz suspended"));
    expect(calls).toHaveLength(1);
  });

  it("Suspend_LastOwnerOfTwo_NamesBoth", async () => {
    server.use(
      meAnswer(),
      userAnswer(
        userDetailOf(ayse, { memberships: lastOwnerOf(northwind, atlas) }),
      ),
    );

    const dialog = await open();

    expect(
      dialog.getByText(
        sentence(
          "Ayşe Yılmaz is the only Owner of Northwind Traders and Atlas Logistics. While they are suspended, nobody there can manage Owners. Make someone else Owner first in each one that needs one.",
        ),
      ),
    ).toBeVisible();
  });

  it("Suspend_LastOwnerOfFour_NamesThreeAndCountsTheRest", async () => {
    server.use(
      meAnswer(),
      userAnswer(
        userDetailOf(ayse, {
          memberships: lastOwnerOf(northwind, atlas, shannon, kestrel),
        }),
      ),
    );

    const dialog = await open();

    expect(
      dialog.getByText(
        sentence(
          "Ayşe Yılmaz is the only Owner of Northwind Traders, Atlas Logistics, Shannon Studio and 1 more. While they are suspended, nobody there can manage Owners. Make someone else Owner first in each one that needs one.",
        ),
      ),
    ).toBeVisible();
  });

  // The reason is required, as for an organisation (the maintainer's decision of 2026-11-08).
  it("Suspend_NoReason_SaysEnterAReasonAndSendsNothing", async () => {
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(mehmet)),
      platformCommand("post", `users/${mehmet.id}/suspend`, [], calls),
    );

    const dialog = await open(mehmet, "Mehmet Kaya");
    await suspend(dialog, "   ");

    expect(await dialog.findByText("Enter a reason.")).toBeVisible();
    expect(calls).toEqual([]);
  });

  it.each([
    [FieldErrorCode.Required, "Enter a reason."],
    [FieldErrorCode.TooLong, "Use at most 500 characters."],
  ])("Suspend_%s_ShowsTheFieldsText", async (code, text) => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(mehmet)),
      platformCommand("post", `users/${mehmet.id}/suspend`, [
        () =>
          problem(400, ErrorCode.RequestValidationFailed, { reason: [code] }),
      ]),
    );

    const dialog = await open(mehmet, "Mehmet Kaya");
    await suspend(dialog);

    expect(await dialog.findByText(text)).toBeVisible();
  });

  it("Suspend_SystemAdmin_SaysRemoveThemFirst", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ciara)),
      platformCommand("post", `users/${ciara.id}/suspend`, [
        () => problem(409, ErrorCode.UserIsSystemAdmin),
      ]),
    );

    const dialog = await open(ciara, "Ciara Byrne");
    await suspend(dialog);

    expect(
      await dialog.findByText(sentence("Ciara Byrne is a system admin.")),
    ).toBeVisible();
    expect(
      dialog.getByText("Remove them from System admins first."),
    ).toBeVisible();
  });

  it("Suspend_NotFound_SaysSoAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(mehmet), reads),
      platformCommand("post", `users/${mehmet.id}/suspend`, [
        () => problem(404, ErrorCode.UserNotFound),
      ]),
    );

    const dialog = await open(mehmet, "Mehmet Kaya");
    await suspend(dialog);

    expect(
      await dialog.findByText("This user no longer exists."),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });

  // In Arabic a Latin name reorders the punctuation around it unless it is isolated (W3C, "Inline markup and bidirectional text").
  // A deleted user can't be suspended (409 user.deleted); the page offers no Suspend, so this is the race of another admin's delete.
  it("Suspend_DeletedMeanwhile_SaysSoAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(mehmet), reads),
      platformCommand("post", `users/${mehmet.id}/suspend`, [
        () => problem(409, ErrorCode.UserDeleted),
      ]),
    );

    const dialog = await open(mehmet, "Mehmet Kaya");
    await suspend(dialog);

    expect(await dialog.findByText("This user was deleted.")).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Suspend_Arabic_IsolatesTheUsersNameInTheTitle", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(mehmet, { memberships: mehmetMemberships })),
    );
    renderWithProviders(<UserPage userId={mehmet.id} />, "ar");

    await userEvent.click(
      await screen.findByRole("button", { name: "إجراءات Mehmet Kaya" }),
    );
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "إيقاف" }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "إيقاف Mehmet Kaya؟",
    });

    // The title isolates the name; so does the sentence under it.
    expect(
      within(within(dialog).getByRole("heading")).getByText("Mehmet Kaya", {
        selector: "bdi",
      }),
    ).toBeVisible();
  });

  it("Suspend_ActiveUserWithoutNames_NamesThemByTheirEmail", async () => {
    server.use(meAnswer(), userAnswer(userDetailOf(unnamed)));

    const dialog = await open(unnamed, "root@starter.example");

    expect(
      within(dialog.getByRole("heading")).getByText("root@starter.example", {
        selector: "bdi",
      }),
    ).toBeVisible();
  });
});
