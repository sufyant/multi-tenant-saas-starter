import {
  ErrorCode,
  type TenantDetailResponse,
  TenantResponseStatus,
} from "@repo/api-client";
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
  commandAnswer,
  dateOf,
  detailOf,
  kestrel,
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

const open = () => openAction(kestrel, "Reopen", "Reopen Kestrel Labs?");

/** GetTenant: closed at first, then what the organisation became meanwhile. */
const detailAnswers = (
  after: Partial<TenantDetailResponse>,
  reads: { count: number } = { count: 0 },
) =>
  http.get(`*/v1/platform/tenants/${kestrel.id}`, () => {
    reads.count += 1;
    return HttpResponse.json(
      reads.count === 1 ? detailOf(kestrel) : detailOf(kestrel, after),
    );
  });

const confirm = (dialog: Awaited<ReturnType<typeof open>>) =>
  userEvent.click(dialog.getByRole("button", { name: "Reopen organisation" }));

describe("ReopenDialog", () => {
  it("Reopen_Success_ToastsAndReadsAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      detailAnswers(
        {
          status: TenantResponseStatus.Active,
          closedAt: null,
          reopenableUntil: null,
        },
        reads,
      ),
      commandAnswer("post", `${kestrel.id}/reopen`, [
        tenantAnswer(kestrel, {
          status: TenantResponseStatus.Active,
          closedAt: null,
        }),
      ]),
    );

    const dialog = await open();
    expect(
      dialog.getByText(
        "It becomes active and its 4 members can sign in again. Invitations cancelled at closing stay cancelled; invite those people again.",
      ),
    ).toBeVisible();
    await confirm(dialog);

    expect(
      await screen.findByText(sentence("Kestrel Labs reopened")),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Reopen_PeriodEnded_NamesTheDateAndReadsAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      detailAnswers({ status: TenantResponseStatus.Purging }, reads),
      commandAnswer("post", `${kestrel.id}/reopen`, [
        () => problem(409, ErrorCode.TenantReopenPeriodEnded),
      ]),
    );

    const dialog = await open();
    await confirm(dialog);

    expect(
      await dialog.findByText(
        `The reopen period ended on ${dateOf(kestrel.reopenableUntil as string)}.`,
      ),
    ).toBeVisible();
    expect(
      dialog.getByText(
        sentence("Kestrel Labs is being deleted. The page has been refreshed."),
      ),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBeGreaterThan(1));
  });

  it("Reopen_NotClosedBecauseReopenedAlready_SaysSo", async () => {
    server.use(
      meAnswer(),
      detailAnswers({ status: TenantResponseStatus.Active }),
      commandAnswer("post", `${kestrel.id}/reopen`, [
        () => problem(409, ErrorCode.TenantNotClosed),
      ]),
    );

    const dialog = await open();
    await confirm(dialog);

    expect(
      await dialog.findByText(sentence("Kestrel Labs is not closed.")),
    ).toBeVisible();
    expect(
      dialog.getByText(
        "Someone reopened it already. The page has been refreshed.",
      ),
    ).toBeVisible();
  });

  it.each([TenantResponseStatus.Purging, TenantResponseStatus.Purged])(
    "Reopen_NotClosedBecause%s_SaysItIsBeingDeleted",
    async (status) => {
      server.use(
        meAnswer(),
        detailAnswers({ status }),
        commandAnswer("post", `${kestrel.id}/reopen`, [
          () => problem(409, ErrorCode.TenantNotClosed),
        ]),
      );

      const dialog = await open();
      await confirm(dialog);

      expect(
        await dialog.findByText(
          sentence("Kestrel Labs is being deleted and can’t be reopened."),
        ),
      ).toBeVisible();
    },
  );
});
