import {
  ErrorCode,
  FieldErrorCode,
  TenantResponseStatus,
} from "@repo/api-client";
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
import { sentence } from "../../../test/render";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const open = () =>
  openAction(northwind, "Suspend", "Suspend Northwind Traders?");

const suspend = async (
  dialog: Awaited<ReturnType<typeof open>>,
  reason: string,
) => {
  await userEvent.type(dialog.getByRole("textbox", { name: "Reason" }), reason);
  await userEvent.click(
    dialog.getByRole("button", { name: "Suspend organisation" }),
  );
};

describe("SuspendDialog", () => {
  it("Suspend_Success_SendsTheReasonAndNamesTheMembers", async () => {
    const reads = { count: 0 };
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      detailAnswer(detailOf(northwind), reads),
      commandAnswer(
        "post",
        `${northwind.id}/suspend`,
        [tenantAnswer(northwind, { status: TenantResponseStatus.Suspended })],
        calls,
      ),
    );

    const dialog = await open();
    expect(
      dialog.getByText(
        "Its 7 members lose access at once. Data and memberships are kept. You can reactivate it at any time.",
      ),
    ).toBeVisible();
    expect(
      dialog.getByText("Required, up to 500 characters. Members never see it."),
    ).toBeVisible();
    await suspend(dialog, "Contract under review");

    expect(
      await screen.findByText(sentence("Northwind Traders suspended")),
    ).toBeVisible();
    expect(screen.getByText("Its 7 members cannot sign in.")).toBeVisible();
    expect(calls.map((call) => call.body)).toEqual([
      { reason: "Contract under review" },
    ]);
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Suspend_NoReason_SaysEnterAReasonAndSendsNothing", async () => {
    const calls: { key: string | null; body: unknown }[] = [];
    server.use(
      meAnswer(),
      detailAnswer(detailOf(northwind)),
      commandAnswer("post", `${northwind.id}/suspend`, [], calls),
    );

    const dialog = await open();
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
      detailAnswer(detailOf(northwind)),
      commandAnswer("post", `${northwind.id}/suspend`, [
        () =>
          problem(400, ErrorCode.RequestValidationFailed, { reason: [code] }),
      ]),
    );

    const dialog = await open();
    await suspend(dialog, "Contract under review");

    expect(await dialog.findByText(text)).toBeVisible();
  });

  it("Suspend_NotActive_SaysSoAndReadsAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      detailAnswer(detailOf(northwind), reads),
      commandAnswer("post", `${northwind.id}/suspend`, [
        () => problem(409, ErrorCode.TenantNotActive),
      ]),
    );

    const dialog = await open();
    await suspend(dialog, "Contract under review");

    expect(
      await dialog.findByText(
        sentence("Northwind Traders cannot be suspended now."),
      ),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });
});
