import {
  BillingResponseBilledBy,
  ErrorCode,
  PaymentResponseState,
  type SubscriptionResponse,
  SubscriptionResponseInterval,
  SubscriptionResponseStatus,
  type TenantBillingResponse,
  TenantResponseStatus,
} from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import {
  burren,
  detailOf,
  meAnswer,
  problem,
} from "../../../test/organisations";
import {
  anyTenantPlanAnswer,
  burrenPlan,
  free,
  planAnswer,
  plansAnswer,
  pro,
  tenantPlanAnswer,
} from "../../../test/plans";
import { renderWithProviders } from "../../../test/render";
import { BillingCard } from "./billing-card";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() =>
  server.use(meAnswer(), plansAnswer(), anyTenantPlanAnswer(), signUpAnswer()),
);
afterEach(() => {
  server.resetHandlers();
  vi.useRealTimers();
});
afterAll(() => server.close());

const active = detailOf(burren, { status: TenantResponseStatus.Active });

/** GetSignUp: the trial a new organisation starts, 14 days of Pro here, which Give trial offers first (4.29). */
function signUpAnswer() {
  return http.get("*/v1/public/sign-up", () =>
    HttpResponse.json({
      enabled: true,
      tenantLimit: 3,
      trial: { plan: { id: pro.id, name: pro.name }, days: 14 },
    }),
  );
}

/** A trial of Pro from 22 Oct to 5 Nov 2026; while it runs, the organisation returns to Free at its end. */
const trialOf = (running: boolean) => ({
  ...burrenPlan(),
  tenantId: active.id,
  trial: {
    plan: { id: pro.id, name: pro.name },
    startedAt: "2026-10-22T10:00:00Z",
    endsAt: "2026-11-05T10:00:00Z",
    returnsTo: running ? { id: free.id, name: free.name } : null,
  },
});

const trialAnswer = (
  running: boolean,
  sent: { method: string; body: unknown }[] = [],
) => [
  tenantPlanAnswer(trialOf(running)),
  http.all(`*/v1/platform/tenants/${active.id}/trial`, async ({ request }) => {
    sent.push({ method: request.method, body: await request.json() });
    return HttpResponse.json(trialOf(true), {
      status: request.method === "POST" ? 201 : 200,
    });
  }),
];

const monthly: SubscriptionResponse = {
  planId: "0198f0a2-0000-7000-8000-0000000000a2",
  interval: SubscriptionResponseInterval.Monthly,
  status: SubscriptionResponseStatus.Active,
  currentPeriodEnd: "2026-12-01T00:00:00Z",
  cancelAtPeriodEnd: false,
  scheduledChange: null,
  payment: null,
  priceChange: null,
  lastPaymentAt: null,
};

const billingAnswer = (answer: TenantBillingResponse, sent: unknown[] = []) => [
  http.get(`*/v1/platform/tenants/${active.id}/billing`, () =>
    HttpResponse.json(answer),
  ),
  http.put(
    `*/v1/platform/tenants/${active.id}/billing/billed-by`,
    async ({ request }) => {
      sent.push(await request.json());
      return HttpResponse.json({
        ...answer,
        billedBy: BillingResponseBilledBy.Contract,
      });
    },
  ),
];

const card = (
  subscription: SubscriptionResponse | null = monthly,
): TenantBillingResponse => ({
  billedBy: BillingResponseBilledBy.Card,
  subscription,
  billingEmail: null,
  customer: null,
});

describe("BillingCard", () => {
  it("BillingCard_PaysByCard_ShowsHowItPaysThePeriodAndPaid", async () => {
    server.use(...billingAnswer(card()));

    renderWithProviders(<BillingCard organisation={active} />);

    expect(
      await screen.findByText(
        "Pays in the app by card. Stripe keeps the card.",
      ),
    ).toBeVisible();
    expect(screen.getByText("Monthly")).toBeVisible();
    expect(screen.getByText("Paid")).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Switch to Contract" }),
    ).toBeVisible();
  });

  // 4.27: the billed people are the members who take a seat; the billing managers are free; the plan's fee per person prices them.
  it("BillingCard_PaysByCard_ShowsTheBilledPeopleThePriceAndTheNextPayment", async () => {
    const team = {
      ...pro,
      prices: { ...pro.prices, perPersonMonthly: 1000 },
    };
    server.use(
      ...billingAnswer(card({ ...monthly, planId: team.id })),
      planAnswer(team),
    );

    renderWithProviders(
      <BillingCard
        organisation={{ ...active, memberCount: 11, freeMemberCount: 1 }}
      />,
    );

    expect(
      await screen.findByText(
        "Monthly · 10 billed people × €10 = €100 · 1 billing manager, free",
      ),
    ).toBeVisible();
    expect(
      screen.getByText("Next payment on 1 Dec 2026: €100 excl. VAT."),
    ).toBeVisible();
  });

  it("BillingCard_YearlyWithABaseFee_PricesTheBaseAndThePeople", async () => {
    const team = {
      ...pro,
      prices: { ...pro.prices, baseYearly: 5000, perPersonYearly: 10000 },
    };
    server.use(
      ...billingAnswer(
        card({
          ...monthly,
          planId: team.id,
          interval: SubscriptionResponseInterval.Yearly,
        }),
      ),
      planAnswer(team),
    );

    renderWithProviders(
      <BillingCard organisation={{ ...active, memberCount: 3 }} />,
    );

    expect(
      await screen.findByText("Yearly · €50 + 3 billed people × €100 = €350"),
    ).toBeVisible();
  });

  it("BillingCard_AFreePlan_PricesItAtZero", async () => {
    server.use(
      ...billingAnswer(card({ ...monthly, planId: pro.id })),
      planAnswer(pro),
    );

    renderWithProviders(<BillingCard organisation={active} />);

    expect(await screen.findByText("Monthly · €0")).toBeVisible();
    expect(
      screen.getByText("Next payment on 1 Dec 2026: €0 excl. VAT."),
    ).toBeVisible();
  });

  // A scheduled end or move has its own row, so the period says no next payment then.
  it("BillingCard_ACancellationScheduled_SaysNoNextPayment", async () => {
    server.use(
      ...billingAnswer(
        card({ ...monthly, planId: pro.id, cancelAtPeriodEnd: true }),
      ),
      planAnswer(pro),
    );

    renderWithProviders(<BillingCard organisation={active} />);

    expect(await screen.findByText("Monthly · €0")).toBeVisible();
    expect(screen.queryByText(/^Next payment/)).not.toBeInTheDocument();
  });

  it("BillingCard_Paid_SaysTheLastPayment", async () => {
    server.use(
      ...billingAnswer(
        card({ ...monthly, lastPaymentAt: "2026-11-01T10:00:00Z" }),
      ),
    );

    renderWithProviders(<BillingCard organisation={active} />);

    expect(
      await screen.findByText("Last payment on 1 Nov 2026."),
    ).toBeVisible();
  });

  // 4.27: the Stripe customer, with its page in Stripe's dashboard in a new tab.
  it("BillingCard_AStripeCustomer_ShowsItsIdAndOpensItInStripe", async () => {
    server.use(
      ...billingAnswer({
        ...card(),
        customer: {
          id: "cus_Q7xN2kL9aB31",
          dashboardUrl:
            "https://dashboard.stripe.com/customers/cus_Q7xN2kL9aB31",
        },
      }),
    );

    renderWithProviders(<BillingCard organisation={active} />);

    expect(await screen.findByText("cus_Q7xN2kL9aB31")).toBeVisible();
    const link = screen.getByRole("link", { name: "Open in Stripe" });
    expect(link).toHaveAttribute(
      "href",
      "https://dashboard.stripe.com/customers/cus_Q7xN2kL9aB31",
    );
    expect(link).toHaveAttribute("target", "_blank");
  });

  it("BillingCard_PaymentRetrying_SaysWhenStripeTriesAgain", async () => {
    server.use(
      ...billingAnswer(
        card({
          ...monthly,
          status: SubscriptionResponseStatus.PastDue,
          payment: {
            state: PaymentResponseState.Retrying,
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

    renderWithProviders(<BillingCard organisation={active} />);

    expect(
      await screen.findByText(
        "The payment of €100 excl. VAT failed. Stripe tries again on 5 Nov 2026. Everything keeps working.",
      ),
    ).toBeVisible();
  });

  it("BillingCard_BillingOff_ShowsNothing", async () => {
    let asked = false;
    server.use(
      http.get(`*/v1/platform/tenants/${active.id}/billing`, () => {
        asked = true;
        return problem(404, "billing.off");
      }),
    );

    renderWithProviders(<BillingCard organisation={active} />);

    await expect.poll(() => asked).toBe(true);
    expect(
      screen.queryByRole("region", { name: "Billing" }),
    ).not.toBeInTheDocument();
  });

  it("SwitchToContract_Confirm_SendsContractAndSaysSo", async () => {
    const user = userEvent.setup();
    const sent: unknown[] = [];
    server.use(...billingAnswer(card(), sent));
    renderWithProviders(<BillingCard organisation={active} />);

    await user.click(
      await screen.findByRole("button", { name: "Switch to Contract" }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: `Mark ${active.name} as Contract?`,
    });
    await user.click(
      within(dialog).getByRole("button", { name: "Mark as Contract" }),
    );

    await expectVisibleToast("Billing changed");
    expect(sent).toEqual([{ billedBy: "contract" }]);
  });

  // 4.28: in a trial, the period is the trial, the payment has none yet, and the trial row says its plan and dates, with Extend trial.
  it("BillingCard_InTrial_ShowsTheTrialItsEndAndExtend", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-11-02T10:00:00Z"));
    server.use(...billingAnswer(card(null)), ...trialAnswer(true));

    renderWithProviders(<BillingCard organisation={active} />);

    expect(
      await screen.findByText(
        "Pro trial ends on 5 Nov 2026. 3 days left. Then it moves to Free.",
      ),
    ).toBeVisible();
    expect(screen.getByText("In trial")).toBeVisible();
    expect(screen.getByText("Pro, 22 Oct to 5 Nov 2026.")).toBeVisible();
    expect(screen.getByRole("button", { name: "Extend trial" })).toBeVisible();
  });

  // 4.29: an organisation that used its trial shows it, and a system admin can give another.
  it("BillingCard_TrialUsed_ShowsItAndOffersGiveTrial", async () => {
    server.use(...billingAnswer(card()), ...trialAnswer(false));

    renderWithProviders(<BillingCard organisation={active} />);

    expect(await screen.findByText("Trial used")).toBeVisible();
    expect(screen.getByText("Pro, 22 Oct to 5 Nov 2026.")).toBeVisible();
    expect(screen.getByRole("button", { name: "Give trial" })).toBeVisible();
  });

  // 4.29: the trial of a new organisation is offered first; it starts today, and the organisation moves back to its plan at its end.
  it("GiveTrial_Confirm_SendsThePlanAndTheDaysAndSaysSo", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-11-02T10:00:00Z"));
    const sent: { method: string; body: unknown }[] = [];
    server.use(...billingAnswer(card()), ...trialAnswer(false, sent));
    renderWithProviders(<BillingCard organisation={active} />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Give trial" }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: `Give trial to ${active.name}`,
    });
    expect(dialog).toHaveTextContent(
      `${active.name} used a trial from 22 Oct to 5 Nov 2026. Only a system admin can give another. It starts today and needs no card.`,
    );
    expect(within(dialog).getByLabelText("Days")).toHaveValue("14");
    expect(
      within(dialog).getByText(
        "Ends on 16 Nov 2026. Then it moves back to Pro, monthly.",
      ),
    ).toBeVisible();
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Give trial" }),
    );

    await expectVisibleToast("Trial given");
    expect(sent).toEqual([
      { method: "POST", body: { planId: pro.id, days: 14 } },
    ]);
  });

  // 4.30: the extra days move the trial's end; the dialog says the new end.
  it("ExtendTrial_Confirm_SendsTheNewEndAndSaysSo", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-11-02T10:00:00Z"));
    const sent: { method: string; body: unknown }[] = [];
    server.use(...billingAnswer(card(null)), ...trialAnswer(true, sent));
    renderWithProviders(<BillingCard organisation={active} />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Extend trial" }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Extend the trial",
    });
    expect(dialog).toHaveTextContent(
      `${active.name} is on a Pro trial until 5 Nov 2026.`,
    );
    await userEvent.type(within(dialog).getByLabelText("Extra days"), "7");
    expect(
      within(dialog).getByText("New end date: 12 Nov 2026."),
    ).toBeVisible();
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Extend trial" }),
    );

    await expectVisibleToast("Trial extended");
    expect(sent).toEqual([
      { method: "PATCH", body: { endsAt: "2026-11-12T10:00:00.000Z" } },
    ]);
  });

  // The API's range: a trial lasts 1 to 365 days, so the dialog says so and sends nothing.
  it("GiveTrial_DaysOutOfRange_SaysTheRangeAndSendsNothing", async () => {
    const sent: { method: string; body: unknown }[] = [];
    server.use(...billingAnswer(card()), ...trialAnswer(false, sent));
    renderWithProviders(<BillingCard organisation={active} />);

    await userEvent.click(
      await screen.findByRole("button", { name: "Give trial" }),
    );
    const dialog = await screen.findByRole("alertdialog");
    await userEvent.clear(within(dialog).getByLabelText("Days"));
    await userEvent.type(within(dialog).getByLabelText("Days"), "400");

    expect(
      within(dialog).getByText("A trial lasts 1 to 365 days."),
    ).toBeVisible();
    expect(
      within(dialog).getByRole("button", { name: "Give trial" }),
    ).toBeDisabled();
    expect(sent).toEqual([]);
  });

  // F48: a refusal saved under the key would answer again, so the next try sends a new key; the trial changed meanwhile, so the screen
  // reads the organisation's plan again (F47).
  it("GiveTrial_RefusedInTrialThenAgain_SendsANewKeyAndReadsThePlanAgain", async () => {
    const keys: (string | null)[] = [];
    const reads = { count: 0 };
    server.use(
      ...billingAnswer(card()),
      tenantPlanAnswer(trialOf(false), reads),
      http.post(`*/v1/platform/tenants/${active.id}/trial`, ({ request }) => {
        keys.push(request.headers.get("Idempotency-Key"));
        return problem(409, ErrorCode.TenantInTrial);
      }),
    );
    renderWithProviders(<BillingCard organisation={active} />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Give trial" }),
    );
    const dialog = await screen.findByRole("alertdialog");
    await vi.waitFor(() =>
      expect(within(dialog).getByLabelText("Days")).toHaveValue("14"),
    );
    const give = within(dialog).getByRole("button", { name: "Give trial" });

    await userEvent.click(give);
    await vi.waitFor(() =>
      expect(dialog).toHaveTextContent(
        "The organisation is in a trial. Extend it, or wait until it ends.",
      ),
    );
    await userEvent.click(give);

    await vi.waitFor(() => expect(keys).toHaveLength(2));
    expect(new Set(keys).size).toBe(2);
    expect(reads.count).toBeGreaterThan(1);
  });
});
