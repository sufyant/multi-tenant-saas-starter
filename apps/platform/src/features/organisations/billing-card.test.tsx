import {
  type BillingResponse,
  BillingResponseBilledBy,
  PaymentResponseState,
  type SubscriptionResponse,
  SubscriptionResponseInterval,
  SubscriptionResponseStatus,
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
import { plansAnswer } from "../../../test/plans";
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

const billingAnswer = (answer: BillingResponse, sent: unknown[] = []) => [
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

const card = (subscription: SubscriptionResponse | null = monthly) => ({
  billedBy: BillingResponseBilledBy.Card,
  subscription,
  billingEmail: null,
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
