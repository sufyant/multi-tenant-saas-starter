import { ErrorCode, UserResponseStatus } from "@repo/api-client";
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
import { openUserAction } from "../../../test/user-page";
import {
  ayse,
  ayseMemberships,
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

const detail = userDetailOf(ayse, { memberships: ayseMemberships });

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
        "They leave every organisation. Their email and names are erased. Activity keeps their entries as Deleted user. This cannot be undone.",
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
            firstName: null,
            lastName: null,
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

    expect(await screen.findByText("Ayşe Yılmaz deleted.")).toBeVisible();
    expect(calls).toEqual([{ key: null, body: null }]);
    expect(
      await screen.findByRole("heading", { level: 1, name: "Deleted user" }),
    ).toBeVisible();
  });

  it("Delete_SystemAdmin_SaysRemoveThemFirstAndLinksToSystemAdmins", async () => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ciara)),
      platformCommand("delete", `users/${ciara.id}`, [
        () => problem(409, ErrorCode.UserIsSystemAdmin),
      ]),
    );

    const dialog = await open(ciara, "Ciara Byrne");
    await confirm(dialog, "ciara@starter.example");

    expect(
      await dialog.findByText("Ciara Byrne is a system admin."),
    ).toBeVisible();
    expect(
      dialog.getByText("Remove them from System admins first."),
    ).toBeVisible();
    expect(
      dialog.getByRole("link", { name: "Open System admins" }),
    ).toHaveAttribute("href", "/settings/system-admins");
  });

  it("Delete_LastOwner_NamesEachOrganisationFromTheMemberships", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      userAnswer(
        userDetailOf(ayse, {
          memberships: [
            membershipOf(northwind, { lastOwner: true }),
            membershipOf(atlas, { lastOwner: true }),
          ],
        }),
        reads,
      ),
      platformCommand("delete", `users/${ayse.id}`, [
        () =>
          problem(409, ErrorCode.UserLastOwner, undefined, [
            northwind.id,
            atlas.id,
          ]),
      ]),
    );

    const dialog = await open();
    await confirm(dialog);

    const refused = await screen.findByRole("alertdialog", {
      name: "Ayşe Yılmaz cannot be deleted",
    });
    expect(refused).toHaveTextContent(
      "They are the last Owner of 2 organisations. Make someone else an Owner there, or close them, then try again.",
    );
    expect(
      screen.getByRole("link", { name: /Northwind Traders/ }),
    ).toHaveAttribute("href", `/organisations/${northwind.id}`);
    expect(
      screen.getByRole("link", { name: /Atlas Logistics/ }),
    ).toHaveAttribute("href", `/organisations/${atlas.id}`);
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Delete_LastOwnerOpenTheOrganisation_OpensTheFirstOneNamed", async () => {
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
      platformCommand("delete", `users/${ayse.id}`, [
        () =>
          problem(409, ErrorCode.UserLastOwner, undefined, [
            northwind.id,
            atlas.id,
          ]),
      ]),
    );

    const dialog = await open();
    await confirm(dialog);
    await userEvent.click(
      await screen.findByRole("button", { name: "Open the organisation" }),
    );

    expect(router.push).toHaveBeenCalledWith(`/organisations/${northwind.id}`);
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
        "Ayşe Yılmaz joined an organisation while this ran.",
      ),
    ).toBeVisible();
    expect(dialog.getByText("Nothing was deleted. Try again.")).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });
});
