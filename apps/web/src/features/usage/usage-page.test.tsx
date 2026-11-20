import { BuiltInRole } from "@repo/api-client";
import { screen, within } from "@testing-library/react";
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
import { billingAnswers, billingOf, teamTenant } from "../../../test/billing";
import {
  northwind,
  problem,
  renderInTenant,
  tenantAnswers,
} from "../../../test/tenant";
import {
  freeMeter,
  teamMeter,
  usageAnswers,
  usageOf,
} from "../../../test/usage";
import { UsagePage } from "./usage-page";

const notFound = vi.hoisted(() => vi.fn());
vi.mock("next/navigation", async (original) => ({
  ...(await original<typeof import("next/navigation")>()),
  notFound,
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  notFound.mockReset();
});
afterAll(() => server.close());

const url = "/northwind/settings/usage";

const as = (role: BuiltInRole, usage = usageOf()) => [
  ...usageAnswers(usage),
  ...billingAnswers(billingOf()),
  ...tenantAnswers(role, undefined, [], teamTenant),
];

const capped = (
  amount: number,
  extraAmount: number,
  reachedAt: string | null = null,
) => ({
  spendingCap: { amount, extraAmount, reachedAt },
});

describe("UsagePage", () => {
  it("UsagePage_Team_ShowsThePeriodTheMeterAndItsPrice", async () => {
    server.use(...as(BuiltInRole.Owner));

    renderPage();

    expect(
      await screen.findByText(
        "What Northwind Traders used this billing period: 1 Nov to 30 Nov 2026.",
      ),
    ).toBeVisible();
    expect(
      screen.getByText(
        "Team gives 10,000 ai.requests a month. The extra costs €1 per 1,000, excl. VAT.",
      ),
    ).toBeVisible();
    const meter = screen.getByRole("meter", {
      name: "7,200 of 10,000 ai.requests",
    });
    expect(meter).toHaveAttribute("aria-valuenow", "7200");
    expect(screen.getByText("72%")).toBeVisible();
    expect(screen.getByText("Resets on 1 Dec")).toBeVisible();
  });

  it("UsagePage_Team_ListsThePastPeriodsWithTheirExtra", async () => {
    server.use(...as(BuiltInRole.Owner));

    renderPage();

    const table = within(
      await screen.findByRole("table", { name: "Past periods" }),
    );
    expect(
      table.getByRole("row", { name: /October 2026 9,100 —/ }),
    ).toBeVisible();
    expect(
      table.getByRole("row", {
        name: /September 2026 10,600 600 · €0.60 excl. VAT/,
      }),
    ).toBeVisible();
  });

  it("UsagePage_NoCap_SaysTheExtraHasNoLimit", async () => {
    server.use(...as(BuiltInRole.Owner));

    renderPage();

    expect(
      await screen.findByText("Off. The extra is charged with no limit."),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Set a cap" })).toBeVisible();
  });

  it("UsagePage_OverTheQuotaWithACap_ShowsTheExtraAndWhatTheCapUsed", async () => {
    server.use(
      ...as(
        BuiltInRole.Owner,
        usageOf({
          meters: [
            teamMeter({
              used: 11400,
              extra: 1400,
              extraAmount: 140,
              quotaReachedAt: "2026-11-12T10:00:00Z",
            }),
          ],
          ...capped(5000, 140),
        }),
      ),
    );

    renderPage();

    expect(
      await screen.findByText("1,400 extra · €1.40 excl. VAT"),
    ).toBeVisible();
    expect(
      screen.getByText("Resets on 1 Dec. The extra is on the next invoice."),
    ).toBeVisible();
    expect(
      screen.getByText(
        "Stop at €50 extra a month, excl. VAT. Used so far: €1.40 excl. VAT.",
      ),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Change cap" })).toBeVisible();
  });

  it("UsagePage_SetACap_SendsTheAmountInMinorUnits", async () => {
    const sent: unknown[] = [];
    server.use(
      http.put(
        `*/v1/tenants/${northwind.id}/usage/spending-cap`,
        async ({ request }) => {
          sent.push(await request.json());
          return new HttpResponse(null, { status: 204 });
        },
      ),
      ...as(BuiltInRole.Owner),
    );
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Set a cap" }));
    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Set a spending cap" }),
    );
    await user.type(dialog.getByLabelText("Extra a month, excl. VAT"), "50");
    await user.click(dialog.getByRole("button", { name: "Set cap" }));

    await expect.poll(() => sent).toEqual([{ amount: 5000 }]);
  });

  it("UsagePage_EmptyFieldWhileACapIsSet_RemovesTheCap", async () => {
    const sent: unknown[] = [];
    server.use(
      http.put(
        `*/v1/tenants/${northwind.id}/usage/spending-cap`,
        async ({ request }) => {
          sent.push(await request.json());
          return new HttpResponse(null, { status: 204 });
        },
      ),
      ...as(BuiltInRole.Owner, usageOf(capped(5000, 0))),
    );
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Change cap" }));
    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Change the spending cap",
      }),
    );
    await user.clear(dialog.getByLabelText("Extra a month, excl. VAT"));
    await user.click(dialog.getByRole("button", { name: "Remove cap" }));

    await expect.poll(() => sent).toEqual([{ amount: null }]);
  });

  it("UsagePage_CapThatIsNoAmount_SaysSoAndSendsNothing", async () => {
    const sent: unknown[] = [];
    server.use(
      http.put(`*/v1/tenants/${northwind.id}/usage/spending-cap`, () => {
        sent.push(true);
        return new HttpResponse(null, { status: 204 });
      }),
      ...as(BuiltInRole.Owner),
    );
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Set a cap" }));
    const dialog = within(await screen.findByRole("alertdialog"));
    await user.type(dialog.getByLabelText("Extra a month, excl. VAT"), "abc");
    await user.click(dialog.getByRole("button", { name: "Set cap" }));

    expect(
      await dialog.findByText("Enter an amount, such as 50."),
    ).toBeVisible();
    expect(sent).toEqual([]);
  });

  it("UsagePage_CapRefused_SaysWhyInTheDialog", async () => {
    server.use(
      http.put(`*/v1/tenants/${northwind.id}/usage/spending-cap`, () =>
        problem(409, "usage.spending_cap_unavailable"),
      ),
      ...as(BuiltInRole.Owner),
    );
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Set a cap" }));
    const dialog = within(await screen.findByRole("alertdialog"));
    await user.type(dialog.getByLabelText("Extra a month, excl. VAT"), "50");
    await user.click(dialog.getByRole("button", { name: "Set cap" }));

    expect(
      await dialog.findByText(
        /^A spending cap is only for an organisation that pays by card\./,
      ),
    ).toBeVisible();
    expect(dialog.getByText("Couldn’t set the spending cap")).toBeVisible();
  });

  it("UsagePage_CapReached_SaysTheMetersStoppedUntilTheReset", async () => {
    server.use(
      ...as(
        BuiltInRole.Owner,
        usageOf({
          meters: [
            teamMeter({
              used: 60000,
              extra: 50000,
              extraAmount: 5000,
              quotaReachedAt: "2026-11-12T10:00:00Z",
            }),
          ],
          ...capped(5000, 5000, "2026-11-24T10:00:00Z"),
        }),
      ),
    );

    renderPage();

    expect(
      await screen.findByText(
        "ai.requests stopped: you reached your spending cap of €50 excl. VAT.",
      ),
    ).toBeVisible();
    expect(
      screen.getByText(
        "They start again on 1 Dec. Raise the cap to use them now.",
      ),
    ).toBeVisible();
    expect(
      screen.getByText("Stopped at the cap · €50 excl. VAT"),
    ).toBeVisible();
    expect(
      screen.getByText(
        "Stop at €50 extra a month, excl. VAT. Reached on 24 Nov.",
      ),
    ).toBeVisible();
  });

  it("UsagePage_FreeQuotaUsed_SaysItStoppedAndOffersTheNextPlan", async () => {
    server.use(
      ...as(
        BuiltInRole.Owner,
        usageOf({
          planName: "Free",
          meters: [freeMeter],
          spendingCapOffered: false,
          pastPeriods: [],
        }),
      ),
    );

    renderPage();

    expect(
      await screen.findByText(
        "You’ve used all 100 ai.requests this month. They reset on 1 Dec.",
      ),
    ).toBeVisible();
    expect(
      await screen.findByText(
        "Change the plan to get more now. Team gives 10,000 a month.",
      ),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Change plan" })).toHaveAttribute(
      "href",
      "/northwind/settings/billing/plans",
    );
    expect(
      screen.getByText("Free gives 100 ai.requests a month. At 100 they stop."),
    ).toBeVisible();
    expect(screen.getByText("Stopped")).toBeVisible();
    expect(screen.getByText("They reset on 1 Dec.")).toBeVisible();
    expect(screen.queryByText("Spending cap")).toBeNull();
  });

  it("UsagePage_NothingUsedYet_SaysSoAndThatPastPeriodsComeLater", async () => {
    server.use(
      ...as(
        BuiltInRole.Owner,
        usageOf({ meters: [teamMeter({ used: 0 })], pastPeriods: [] }),
      ),
    );

    renderPage();

    expect(
      await screen.findByText("Resets on 1 Dec. Nothing used yet this period."),
    ).toBeVisible();
    expect(
      screen.getByText("Past periods appear here after the first month."),
    ).toBeVisible();
  });

  it("UsagePage_BillingManager_SeesTheSamePage", async () => {
    server.use(...as(BuiltInRole.BillingManager));

    renderPage();

    expect(
      await screen.findByRole("meter", { name: "7,200 of 10,000 ai.requests" }),
    ).toBeVisible();
  });

  it("UsagePage_ViewerWhoDoesNotManageBilling_SaysPermissionDenied", async () => {
    server.use(...as(BuiltInRole.Admin));

    renderPage();

    expect(await screen.findByText("You can’t open this page")).toBeVisible();
  });

  it("UsagePage_BillingOff_IsNotFound", async () => {
    server.use(
      ...usageAnswers(usageOf()),
      ...tenantAnswers(BuiltInRole.Owner, undefined, [], teamTenant),
    );

    renderPage();

    await vi.waitFor(() => expect(notFound).toHaveBeenCalled());
  });

  it("UsagePage_PlanWithoutAMeter_IsNotFound", async () => {
    server.use(...as(BuiltInRole.Owner, usageOf({ meters: [] })));

    renderPage();

    await vi.waitFor(() => expect(notFound).toHaveBeenCalled());
  });

  it("UsagePage_ErrorAnswer_OffersToTryAgain", async () => {
    server.use(
      http.get(`*/v1/tenants/${northwind.id}/usage`, () =>
        HttpResponse.json({}, { status: 500 }),
      ),
      ...as(BuiltInRole.Owner),
    );

    renderPage();

    expect(await screen.findByText("Couldn’t load usage")).toBeVisible();
    expect(screen.getByRole("button", { name: "Try again" })).toBeVisible();
  });
});

function renderPage() {
  return renderInTenant(<UsagePage />, { url });
}
