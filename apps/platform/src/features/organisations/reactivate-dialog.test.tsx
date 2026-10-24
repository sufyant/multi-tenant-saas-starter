import { ErrorCode, TenantResponseStatus } from "@repo/api-client";
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
  atlas,
  commandAnswer,
  detailAnswer,
  detailOf,
  meAnswer,
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
  openAction(atlas, "Reactivate", "Reactivate Atlas Logistics?");

const confirm = (dialog: Awaited<ReturnType<typeof open>>) =>
  userEvent.click(
    dialog.getByRole("button", { name: "Reactivate organisation" }),
  );

describe("ReactivateDialog", () => {
  it("Reactivate_Success_ToastsAndReadsAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      detailAnswer(detailOf(atlas), reads),
      commandAnswer("post", `${atlas.id}/reactivate`, [
        tenantAnswer(atlas, {
          status: TenantResponseStatus.Active,
          suspensionReason: null,
        }),
      ]),
    );

    const dialog = await open();
    expect(
      dialog.getByText(
        "Its 9 members can sign in again at once. The reason is cleared. The suspension stays in Activity.",
      ),
    ).toBeVisible();
    await confirm(dialog);

    expect(
      await screen.findByText(sentence("Atlas Logistics reactivated")),
    ).toBeVisible();
    expect(screen.getByText("Its 9 members can sign in again.")).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Reactivate_NotActive_SaysItIsClosedAndReadsAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      detailAnswer(detailOf(atlas), reads),
      commandAnswer("post", `${atlas.id}/reactivate`, [
        () => problem(409, ErrorCode.TenantNotActive),
      ]),
    );

    const dialog = await open();
    await confirm(dialog);

    expect(
      await dialog.findByText(sentence("Atlas Logistics is closed.")),
    ).toBeVisible();
    expect(
      dialog.getByText(
        "A closed organisation is reopened, not reactivated. Use Reopen before its deletion date.",
      ),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });
});
