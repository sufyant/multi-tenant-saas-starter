import {
  BillingResponseBilledBy,
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
} from "vitest";
import {
  burren,
  detailOf,
  meAnswer,
  problem,
} from "../../../test/organisations";
import { planAnswer, plansAnswer, pro } from "../../../test/plans";
import { renderWithProviders } from "../../../test/render";
import { BillingCard } from "./billing-card";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => server.use(meAnswer(), plansAnswer()));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const active = detailOf(burren, { status: TenantResponseStatus.Active });

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
});
