import {
  BillingResponseBilledBy,
  BuiltInRole,
  type UsageResponse,
} from "@repo/api-client";
import { screen } from "@testing-library/react";
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
  billingAnswers,
  billingOf,
  businessTrialTenant,
  teamMonthly,
  teamTenant,
} from "../../test/billing";
import { renderInTenant, tenantAnswers } from "../../test/tenant";
import { freeMeter, teamMeter, usageAnswers, usageOf } from "../../test/usage";
import { BillingBar } from "./billing-bar";
import { useUsage } from "./usage";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  vi.useRealTimers();
});
afterAll(() => server.close());

const answers = (usage: UsageResponse, billing = billingOf()) => [
  ...usageAnswers(usage),
  ...billingAnswers(billing),
  ...tenantAnswers(BuiltInRole.Owner, undefined, [], teamTenant),
];

/** Northwind in its Business trial, which no payment ended: no subscription yet. */
const inTrial = (usage: UsageResponse = usageOf({ meters: [] })) => [
  ...usageAnswers(usage),
  ...billingAnswers(billingOf(null)),
  ...tenantAnswers(BuiltInRole.Owner, undefined, [], businessTrialTenant),
];

const capReached = usageOf({
  meters: [
    teamMeter({
      used: 60000,
      extra: 50000,
      extraAmount: 5000,
      quotaReachedAt: "2026-11-12T10:00:00Z",
    }),
  ],
  spendingCap: {
    amount: 5000,
    extraAmount: 5000,
    reachedAt: "2026-11-24T10:00:00Z",
  },
});

const freeUsedUp = usageOf({
  planName: "Free",
  meters: [freeMeter],
  spendingCapOffered: false,
});

describe("BillingBar", () => {
  it("BillingBar_AMeterAt80Percent_SaysSoWithViewUsage", async () => {
    server.use(...answers(usageOf({ meters: [teamMeter({ used: 8000 })] })));

    renderBar();

    expect(
      await screen.findByText(
        "You’ve used 80% of this month’s ai.requests: 8,000 of 10,000.",
      ),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "View usage" })).toHaveAttribute(
      "href",
      "/northwind/settings/usage",
    );
  });

  it("BillingBar_AChargedMeterUsedUp_SaysTheExtraCostsWithViewUsage", async () => {
    server.use(
      ...answers(
        usageOf({
          meters: [
            teamMeter({
              used: 10200,
              extra: 200,
              extraAmount: 20,
              quotaReachedAt: "2026-11-12T10:00:00Z",
            }),
          ],
        }),
      ),
    );

    renderBar();

    expect(
      await screen.findByText(
        "You’ve used all 10,000 ai.requests. The extra costs €1 per 1,000, excl. VAT.",
      ),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "View usage" })).toBeVisible();
  });

  it("BillingBar_AStopMeterUsedUp_SaysItResetsWithChangePlan", async () => {
    server.use(...answers(freeUsedUp, billingOf(null)));

    renderBar();

    expect(
      await screen.findByText(
        "You’ve used all 100 ai.requests this month. They reset on 1 Dec.",
      ),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "Change plan" })).toHaveAttribute(
      "href",
      "/northwind/settings/billing/plans",
    );
  });

  it("BillingBar_AStopMeterUsedUpOnAContract_OffersViewUsage", async () => {
    server.use(
      ...answers(
        freeUsedUp,
        billingOf(null, { billedBy: BillingResponseBilledBy.Contract }),
      ),
    );

    renderBar();

    await screen.findByText(
      "You’ve used all 100 ai.requests this month. They reset on 1 Dec.",
    );
    expect(screen.getByRole("link", { name: "View usage" })).toBeVisible();
  });

  it("BillingBar_SpendingCapReached_SaysTheMetersStoppedWithChangeCap", async () => {
    server.use(...answers(capReached));

    renderBar();

    expect(
      await screen.findByText(
        "ai.requests stopped: you reached your spending cap of €50 excl. VAT. They reset on 1 Dec.",
      ),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "Change cap" })).toHaveAttribute(
      "href",
      "/northwind/settings/usage",
    );
  });

  it("BillingBar_OnBilling_StillShowsTheUsageNotice", async () => {
    server.use(...answers(capReached));

    renderBar({ onBilling: true });

    expect(
      await screen.findByText(/stopped: you reached your spending cap/),
    ).toBeVisible();
  });

  it("BillingBar_OnUsage_ShowsNoUsageNotice", async () => {
    server.use(...answers(capReached));

    renderBar({ onUsage: true });

    await screen.findByText("Bar done");
    expect(screen.queryByText(/stopped: you reached/)).toBeNull();
  });

  it("BillingBar_PaymentRetryingAndCapReached_ShowsThePaymentFirst", async () => {
    server.use(
      ...answers(
        capReached,
        billingOf({
          ...teamMonthly,
          payment: {
            state: "retrying",
            failedAt: "2026-11-01T10:00:00Z",
            attempts: 1,
            nextAttemptAt: "2026-11-05T10:00:00Z",
            graceEndsAt: null,
            movedAt: null,
            amountDue: 10000,
            currency: "EUR",
            card: null,
          },
        }),
      ),
    );

    renderBar();

    expect(
      await screen.findByText(
        "We couldn’t take your payment. We’ll try again on 5 Nov 2026.",
      ),
    ).toBeVisible();
    expect(screen.queryByText(/stopped: you reached/)).toBeNull();
  });

  // 1.31: a running trial says its end, with Choose a plan, which opens Change plan.
  it("BillingBar_InTrial_SaysItsEndWithChooseAPlan", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-10-26T10:00:00Z"));
    server.use(...inTrial());

    renderBar();

    expect(
      await screen.findByText("Your Business trial ends on 5 Nov 2026."),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "Choose a plan" })).toHaveAttribute(
      "href",
      "/northwind/settings/billing/plans",
    );
  });

  // 2.31: 3 days before its end, the trial says the days left and the plan that comes after it.
  it("BillingBar_TrialEndsIn3Days_SaysTheDaysAndThePlanAfterIt", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-11-02T10:00:00Z"));
    server.use(...inTrial());

    renderBar();

    expect(
      await screen.findByText(
        "Your Business trial ends in 3 days. Then Northwind Traders moves to Free.",
      ),
    ).toBeVisible();
  });

  it("BillingBar_InTrialWithAMeterAt80Percent_ShowsTheTrialFirst", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-10-26T10:00:00Z"));
    server.use(...inTrial(usageOf({ meters: [teamMeter({ used: 8000 })] })));

    renderBar();

    expect(
      await screen.findByText("Your Business trial ends on 5 Nov 2026."),
    ).toBeVisible();
    expect(screen.queryByText(/You’ve used 80%/)).toBeNull();
  });

  it("BillingBar_InTrialOnBilling_ShowsNoTrial", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-10-26T10:00:00Z"));
    server.use(...inTrial());

    renderBar({ onBilling: true });

    expect(await screen.findByText("Bar done")).toBeVisible();
    expect(screen.queryByText(/trial ends/)).toBeNull();
  });
});

function renderBar({
  onUsage = false,
  onBilling = false,
}: {
  onUsage?: boolean;
  onBilling?: boolean;
} = {}) {
  return renderInTenant(
    <>
      <BillingBar onBilling={onBilling} onUsage={onUsage} />
      <BarDone />
    </>,
    { url: "/northwind/settings/members" },
  );
}

/** Shows once GetUsage has answered, so a test of no notice does not pass before the notice could show. */
function BarDone() {
  const usage = useUsage();
  return usage.isSuccess ? <p>Bar done</p> : null;
}
