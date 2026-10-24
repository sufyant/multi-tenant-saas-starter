import { ErrorCode, FieldErrorCode } from "@repo/api-client";
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
  ayse,
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

const open = () =>
  openUserAction(ayse, "Ayşe Yılmaz", "Rename", "Rename Ayşe Yılmaz");

const lastName = async (
  dialog: Awaited<ReturnType<typeof open>>,
  value: string,
) => {
  const field = dialog.getByRole("textbox", { name: "Last name" });
  await userEvent.clear(field);
  if (value) await userEvent.type(field, value);
  await userEvent.click(dialog.getByRole("button", { name: "Rename user" }));
};

describe("RenameUserDialog", () => {
  it("Rename_Success_SendsTrimmedNamesAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse), reads),
      platformCommand(
        "patch",
        `users/${ayse.id}`,
        [userResponse(ayse, { lastName: "Yılmaz Demir" })],
        calls,
      ),
    );

    const dialog = await open();
    expect(dialog.getByRole("textbox", { name: "First name" })).toHaveValue(
      "Ayşe",
    );
    expect(
      dialog.getByText(
        "Fixes a typo or a name change. The new names show in every organisation at once. Past Activity shows the new names too.",
      ),
    ).toBeVisible();
    await lastName(dialog, "  Yılmaz Demir ");

    expect(
      await screen.findByText("Renamed to Ayşe Yılmaz Demir."),
    ).toBeVisible();
    expect(calls).toEqual([
      { key: null, body: { firstName: "Ayşe", lastName: "Yılmaz Demir" } },
    ]);
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Rename_EmptyLastName_SaysEnterALastNameAndSendsNothing", async () => {
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse)),
      platformCommand("patch", `users/${ayse.id}`, [], calls),
    );

    const dialog = await open();
    await lastName(dialog, "   ");

    expect(await dialog.findByText("Enter a last name.")).toBeVisible();
    expect(calls).toEqual([]);
  });

  it.each([
    [FieldErrorCode.Required, "firstName", "Enter a first name."],
    [FieldErrorCode.TooLong, "lastName", "Use 1 to 100 characters."],
  ])("Rename_%sOn%s_ShowsTheFieldsText", async (code, field, text) => {
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse)),
      platformCommand("patch", `users/${ayse.id}`, [
        () =>
          problem(400, ErrorCode.RequestValidationFailed, { [field]: [code] }),
      ]),
    );

    const dialog = await open();
    await lastName(dialog, "Demir");

    expect(await dialog.findByText(text)).toBeVisible();
  });

  it("Rename_Deleted_SaysSoAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse), reads),
      platformCommand("patch", `users/${ayse.id}`, [
        () => problem(409, ErrorCode.UserDeleted),
      ]),
    );

    const dialog = await open();
    await lastName(dialog, "Demir");

    expect(await dialog.findByText("This user was deleted.")).toBeVisible();
    expect(dialog.getByText("A deleted user has no name.")).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });
});
