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
import { openAction } from "../../../test/organisation-page";
import {
  commandAnswer,
  detailAnswer,
  detailOf,
  meAnswer,
  northwind,
  problem,
  tenantAnswer,
} from "../../../test/organisations";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const open = () => openAction(northwind, "Rename", "Rename Northwind Traders");

const rename = async (
  dialog: Awaited<ReturnType<typeof open>>,
  name: string,
) => {
  const field = dialog.getByRole("textbox", { name: "Name" });
  await userEvent.clear(field);
  await userEvent.type(field, name);
  await userEvent.click(
    dialog.getByRole("button", { name: "Rename organisation" }),
  );
};

describe("RenameDialog", () => {
  it("Rename_Success_SendsTheNameAndReadsAgain", async () => {
    const reads = { count: 0 };
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      detailAnswer(detailOf(northwind), reads),
      commandAnswer(
        "patch",
        northwind.id,
        [tenantAnswer(northwind, { name: "Northwind Traders Ltd" })],
        calls,
      ),
    );

    const dialog = await open();
    expect(dialog.getByText("northwind")).toBeVisible();
    await rename(dialog, "Northwind Traders Ltd");

    expect(
      await screen.findByText("Renamed to Northwind Traders Ltd."),
    ).toBeVisible();
    expect(calls.map((call) => call.body)).toEqual([
      { name: "Northwind Traders Ltd" },
    ]);
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it.each([
    [
      "TenantNameInvalid",
      () => problem(400, ErrorCode.TenantNameInvalid),
      "Enter a name.",
    ],
    [
      "TooLong",
      () =>
        problem(400, ErrorCode.RequestValidationFailed, {
          name: [FieldErrorCode.TooLong],
        }),
      "Use 1 to 100 characters.",
    ],
  ])("Rename_%s_ShowsTheFieldsText", async (_, answer, text) => {
    server.use(
      meAnswer(),
      detailAnswer(detailOf(northwind)),
      commandAnswer("patch", northwind.id, [answer]),
    );

    const dialog = await open();
    await rename(dialog, "Northwind");

    expect(await dialog.findByText(text)).toBeVisible();
  });

  it("Rename_NotActive_SaysSoAndReadsAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      detailAnswer(detailOf(northwind), reads),
      commandAnswer("patch", northwind.id, [
        () => problem(409, ErrorCode.TenantNotActive),
      ]),
    );

    const dialog = await open();
    await rename(dialog, "Northwind");

    expect(
      await dialog.findByText("Northwind Traders cannot be renamed now."),
    ).toBeVisible();
    expect(dialog.getByText("It is closed or being deleted.")).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });
});
