import { ErrorCode, TenantResponseStatus } from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
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
  dateOf,
  detailOf,
  inDays,
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
  openAction(atlas, "Close organisation", "Close Atlas Logistics?");

const closedAt = inDays(0);
const reopenableUntil = inDays(30);

/** GetTenant: suspended before the closure, closed after it. */
const detailAnswers = (reads: { count: number }) =>
  http.get(`*/v1/platform/tenants/${atlas.id}`, () => {
    reads.count += 1;
    return HttpResponse.json(
      reads.count === 1
        ? detailOf(atlas)
        : detailOf(atlas, {
            status: TenantResponseStatus.Closed,
            closedAt,
            reopenableUntil,
          }),
    );
  });

describe("CloseDialog", () => {
  it("Close_TheSlugTyped_ClosesAndToastsTheApisDate", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      detailAnswers(reads),
      commandAnswer("post", `${atlas.id}/close`, [
        tenantAnswer(atlas, { status: TenantResponseStatus.Closed, closedAt }),
      ]),
    );

    const dialog = await open();
    expect(
      dialog.getByText(
        "Its 9 members lose access at once. Pending invitations are cancelled. You can reopen it for a limited time. After that it is deleted with all its data.",
      ),
    ).toBeVisible();
    const button = dialog.getByRole("button", { name: "Close organisation" });
    expect(button).toBeDisabled();
    await userEvent.type(
      dialog.getByRole("textbox", { name: "Type atlas-logistics to confirm" }),
      "atlas-logistics",
    );
    await userEvent.click(button);

    await expectVisibleToast(sentence("Atlas Logistics closed"));
    expect(
      screen.getByText(
        `You can reopen it until ${dateOf(reopenableUntil)}. Pending invitations were cancelled.`,
      ),
    ).toBeVisible();
    expect(
      await screen.findByText(
        `Closed on ${dateOf(closedAt)}. You can reopen it until ${dateOf(reopenableUntil)}. After that it is deleted.`,
      ),
    ).toBeVisible();
  });

  it("Close_NotActive_SaysItIsBeingDeletedAndReadsAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      detailAnswers(reads),
      commandAnswer("post", `${atlas.id}/close`, [
        () => problem(409, ErrorCode.TenantNotActive),
      ]),
    );

    const dialog = await open();
    await userEvent.type(
      dialog.getByRole("textbox", { name: "Type atlas-logistics to confirm" }),
      "atlas-logistics",
    );
    await userEvent.click(
      dialog.getByRole("button", { name: "Close organisation" }),
    );

    expect(
      await dialog.findByText(sentence("Atlas Logistics cannot be closed.")),
    ).toBeVisible();
    expect(dialog.getByText(sentence("It is being deleted."))).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });
});
