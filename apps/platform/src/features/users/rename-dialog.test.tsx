import { ErrorCode, FieldErrorCode } from "@repo/api-client";
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
import { meAnswer, problem } from "../../../test/organisations";
import { sentence } from "../../../test/render";
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

const rename = async (
  dialog: Awaited<ReturnType<typeof open>>,
  value: string,
) => {
  const field = dialog.getByRole("textbox", { name: "Name" });
  await userEvent.clear(field);
  await userEvent.type(field, value);
  await userEvent.click(dialog.getByRole("button", { name: "Rename user" }));
};

describe("RenameUserDialog", () => {
  it("Rename_Success_SendsTheTrimmedNameAndReadsTheUserAgain", async () => {
    const reads = { count: 0 };
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse), reads),
      platformCommand(
        "patch",
        `users/${ayse.id}`,
        [userResponse(ayse, { name: "Ayşe Yılmaz Demir" })],
        calls,
      ),
    );

    const dialog = await open();
    expect(dialog.getByRole("textbox", { name: "Name" })).toHaveValue(
      "Ayşe Yılmaz",
    );
    expect(dialog.getAllByRole("textbox")).toHaveLength(1);
    expect(
      dialog.getByText(
        "Fixes a typo or a name change. The new name shows in every organisation at once. Past Activity shows the new name too.",
      ),
    ).toBeVisible();
    await rename(dialog, "  Ayşe Yılmaz Demir ");

    await expectVisibleToast(sentence("Renamed to Ayşe Yılmaz Demir."));
    expect(calls).toEqual([{ key: null, body: { name: "Ayşe Yılmaz Demir" } }]);
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Rename_EmptyName_SaysEnterANameAndSendsNothing", async () => {
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      userAnswer(userDetailOf(ayse)),
      platformCommand("patch", `users/${ayse.id}`, [], calls),
    );

    const dialog = await open();
    await rename(dialog, "   ");

    expect(await dialog.findByText("Enter a name.")).toBeVisible();
    expect(calls).toEqual([]);
  });

  it.each([
    [FieldErrorCode.Required, "name", "Enter a name."],
    [FieldErrorCode.TooLong, "name", "Use 1 to 100 characters."],
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
    await rename(dialog, "Ayşe Demir");

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
    await rename(dialog, "Ayşe Demir");

    expect(
      await dialog.findByText(sentence("This user was deleted.")),
    ).toBeVisible();
    expect(dialog.getByText("A deleted user has no name.")).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });
});
