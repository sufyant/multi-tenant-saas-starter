import {
  BillingResponseBilledBy,
  BuiltInRole,
  PaymentResponseState,
  SubscriptionResponseStatus,
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
  describe,
  expect,
  it,
  vi,
} from "vitest";
import {
  billingAnswers,
  billingOf,
  freePlan,
  teamMonthly,
  teamPlan,
  teamTenant,
} from "../../../test/billing";
import {
  northwind,
  permissionsOf,
  renderInTenant,
  tenantAnswers,
} from "../../../test/tenant";
import { BillingPage } from "./billing-page";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  vi.restoreAllMocks();
});
afterAll(() => server.close());

const url = "/northwind/settings/billing";

const as = (
  role: BuiltInRole,
  billing = billingOf(),
  permissions = permissionsOf[role],
) => [
  ...billingAnswers(billing),
  ...tenantAnswers(role, permissions, [], teamTenant),
];

/** The value beside a term of the plan's rows. */
const valueBeside = (term: string) =>
  screen.getByText(term, { selector: "dt" }).nextElementSibling;

describe("BillingPage", () => {
  it("BillingPage_TeamMonthly_ShowsThePlanItsPriceAndTheNextPayment", async () => {
    server.use(...as(BuiltInRole.Owner));

    renderInTenant(<BillingPage />, { url });

    expect(
      await screen.findByRole("heading", { level: 2, name: /^Team/ }),
    ).toBeVisible();
    expect(
      await screen.findByText(
        "€10 per person × 10 people = €100 a month, excl. VAT",
      ),
    ).toBeVisible();
    expect(
      screen.getByText("Grace Okafor (Billing manager) is free."),
    ).toBeVisible();
    expect(
      screen.getByText("Next payment on 1 Dec 2026: €100 excl. VAT."),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Change plan" })).toHaveAttribute(
      "href",
      "/northwind/settings/billing/plans",
    );
    expect(screen.getByRole("button", { name: "Cancel plan" })).toBeVisible();
  });

  it("BillingPage_TeamMonthly_CountsTheSeatsWithoutTheBillingManager", async () => {
    server.use(...as(BuiltInRole.Owner));

    renderInTenant(<BillingPage />, { url });

    expect(
      await screen.findByText(
        "11 members, 1 of them a billing manager who uses no seat, and 2 pending invitations: 12 of 15 seats.",
      ),
    ).toBeVisible();
    expect(valueBeside("Seats")).toHaveTextContent("12 of 15");
    expect(valueBeside("Custom roles")).toHaveTextContent("4 of 5");
    expect(valueBeside("Custom roles")).toHaveTextContent("Your plan gives 5.");
  });

  it("BillingPage_BillingManager_SeesTheSamePage", async () => {
    server.use(...as(BuiltInRole.BillingManager));

    renderInTenant(<BillingPage />, { url });

    expect(
      await screen.findByText(
        "€10 per person × 10 people = €100 a month, excl. VAT",
      ),
    ).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Manage payment details" }),
    ).toBeVisible();
  });

  it("BillingPage_ViewerWhoDoesNotManageBilling_SaysPermissionDenied", async () => {
    server.use(...as(BuiltInRole.Admin));

    renderInTenant(<BillingPage />, { url });

    expect(await screen.findByText("You can’t open this page")).toBeVisible();
  });

  it("BillingPage_PaymentRetrying_SaysWhenStripeTriesAgainAndOffersTheCard", async () => {
    server.use(
      ...as(
        BuiltInRole.Owner,
        billingOf({
          ...teamMonthly,
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
            card: { brand: "visa", last4: "4242" },
          },
        }),
      ),
    );

    renderInTenant(<BillingPage />, { url });

    const banner = await screen.findByText(
      "We couldn’t take your payment. We’ll try again on 5 Nov 2026.",
    );
    expect(banner).toBeVisible();
    expect(
      screen.getByText(
        "Stripe tried Visa ending in 4242 on 1 Nov 2026. Everything keeps working. Update your card to pay now.",
      ),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Update card" })).toBeVisible();
    expect(
      screen.getByText("Payment due since 1 Nov 2026: €100 excl. VAT."),
    ).toBeVisible();
    expect(
      screen.queryByRole("button", { name: "Cancel plan" }),
    ).not.toBeInTheDocument();
  });

  it("BillingPage_PaymentInGrace_SaysTheDayThePlanMovesToTheDefault", async () => {
    server.use(
      ...as(
        BuiltInRole.Owner,
        billingOf({
          ...teamMonthly,
          status: SubscriptionResponseStatus.PastDue,
          payment: {
            state: PaymentResponseState.Grace,
            failedAt: "2026-11-01T10:00:00Z",
            attempts: 4,
            nextAttemptAt: null,
            graceEndsAt: "2026-11-20T10:00:00Z",
            movedAt: null,
            amountDue: 10000,
            currency: "EUR",
            card: { brand: "visa", last4: "4242" },
          },
        }),
      ),
    );

    renderInTenant(<BillingPage />, { url });

    expect(
      await screen.findByText(
        "Your plan moves to Free on 20 Nov 2026 unless you pay.",
      ),
    ).toBeVisible();
    expect(
      screen.getByText(
        "We tried Visa ending in 4242 4 times since 1 Nov 2026. Everything keeps working until 20 Nov 2026. Nothing is deleted.",
      ),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Pay now" })).toBeVisible();
  });

  it("BillingPage_MovedToTheDefaultPlan_OffersToPayAndGetThePlanBack", async () => {
    server.use(
      ...billingAnswers(
        billingOf({
          ...teamMonthly,
          status: SubscriptionResponseStatus.PastDue,
          payment: {
            state: PaymentResponseState.MovedToDefault,
            failedAt: "2026-11-01T10:00:00Z",
            attempts: 4,
            nextAttemptAt: null,
            graceEndsAt: "2026-11-20T10:00:00Z",
            movedAt: "2026-11-20T10:00:00Z",
            amountDue: 10000,
            currency: "EUR",
            card: null,
          },
        }),
      ),
      ...tenantAnswers(BuiltInRole.Owner, permissionsOf.owner, [], {
        ...teamTenant,
        planName: "Free",
      }),
    );

    renderInTenant(<BillingPage />, { url });

    expect(
      await screen.findByText(
        "Northwind Traders moved to Free on 20 Nov 2026.",
      ),
    ).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Pay €100 and get Team back" }),
    ).toBeVisible();
    expect(
      screen.getByText("Pay the open invoice to get Team back at once."),
    ).toBeVisible();
  });

  it("BillingPage_CancellationScheduled_UndoKeepsThePlan", async () => {
    const user = userEvent.setup();
    let undone = false;
    server.use(
      http.delete(
        `*/v1/tenants/${northwind.id}/billing/scheduled-change`,
        () => {
          undone = true;
          return new HttpResponse(null, { status: 204 });
        },
      ),
      ...as(
        BuiltInRole.Owner,
        billingOf({ ...teamMonthly, cancelAtPeriodEnd: true }),
      ),
    );
    renderInTenant(<BillingPage />, { url });

    expect(
      await screen.findByText("On 1 Dec 2026 you move to Free."),
    ).toBeVisible();
    expect(
      screen.getByText("Team ends on 1 Dec 2026. No more payments."),
    ).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Undo" }));

    await expectVisibleToast("Cancellation undone");
    expect(undone).toBe(true);
  });

  it("BillingPage_PriceChangeTold_SaysTheNewFeeAndTheNewTotal", async () => {
    server.use(
      ...as(
        BuiltInRole.Owner,
        billingOf({
          ...teamMonthly,
          priceChange: {
            movesAt: "2027-02-01T00:00:00Z",
            oldPrices: teamPlan.prices,
            newPrices: { ...teamPlan.prices, perPersonMonthly: 1200 },
          },
        }),
      ),
    );

    renderInTenant(<BillingPage />, { url });

    expect(
      await screen.findByText(
        "From 1 Feb 2027 the Team fee per person goes from €10 to €12 a month, excl. VAT.",
      ),
    ).toBeVisible();
    expect(
      await screen.findByText(
        "For your 10 people: €120 a month instead of €100, excl. VAT. Nothing changes before 1 Feb 2027. You can change or cancel your plan at any time.",
      ),
    ).toBeVisible();
    expect(
      screen.getByText(
        "From 1 Feb 2027: €12 per person × 10 people = €120 a month, excl. VAT.",
      ),
    ).toBeVisible();
  });

  it("BillingPage_Contract_ShowsThePlanAndLimitsAndARequest", async () => {
    server.use(
      ...as(
        BuiltInRole.Owner,
        billingOf(null, { billedBy: BillingResponseBilledBy.Contract }),
      ),
    );

    renderInTenant(<BillingPage />, { url });

    expect(
      await screen.findByText("Your plan is billed by contract."),
    ).toBeVisible();
    expect(
      screen.getByText("Northwind Traders’s plan and limits."),
    ).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Request a plan change" }),
    ).toBeVisible();
    expect(screen.queryByText("Billing email")).not.toBeInTheDocument();
    expect(screen.queryByText("Payment details")).not.toBeInTheDocument();
  });

  it("BillingPage_NeverPaid_SaysNoCardYetAndOffersChangePlan", async () => {
    server.use(
      ...billingAnswers(billingOf(null, { billingEmail: null })),
      ...tenantAnswers(BuiltInRole.Owner, permissionsOf.owner, [], {
        ...teamTenant,
        planName: freePlan.name,
      }),
    );

    renderInTenant(<BillingPage />, { url });

    expect(
      await screen.findByText("No card yet. You add one when you pay."),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Change plan" })).toBeVisible();
    expect(
      screen.queryByRole("button", { name: "Manage payment details" }),
    ).not.toBeInTheDocument();
  });

  it("SaveBillingEmail_ANewAddress_SendsItAndSaysSo", async () => {
    const user = userEvent.setup();
    const sent: unknown[] = [];
    server.use(
      http.put(
        `*/v1/tenants/${northwind.id}/billing/email`,
        async ({ request }) => {
          sent.push(await request.json());
          return new HttpResponse(null, { status: 204 });
        },
      ),
      ...as(BuiltInRole.Owner),
    );
    renderInTenant(<BillingPage />, { url });
    const field = await screen.findByRole("textbox", { name: "Billing email" });
    const save = screen.getByRole("button", { name: "Save" });
    expect(save).toBeDisabled();

    await user.clear(field);
    await user.type(field, "invoices@northwind.example");
    await user.click(save);

    await expectVisibleToast("Billing email saved");
    expect(sent).toEqual([{ address: "invoices@northwind.example" }]);
  });

  it("SaveBillingEmail_AnEmptyField_RemovesTheAddress", async () => {
    const user = userEvent.setup();
    const sent: unknown[] = [];
    server.use(
      http.put(
        `*/v1/tenants/${northwind.id}/billing/email`,
        async ({ request }) => {
          sent.push(await request.json());
          return new HttpResponse(null, { status: 204 });
        },
      ),
      ...as(BuiltInRole.Owner),
    );
    renderInTenant(<BillingPage />, { url });

    await user.clear(
      await screen.findByRole("textbox", { name: "Billing email" }),
    );
    await user.click(screen.getByRole("button", { name: "Save" }));

    await expectVisibleToast("Billing email removed");
    expect(sent).toEqual([{ address: null }]);
  });

  it("SaveBillingEmail_NotAnAddress_SaysWhatToEnter", async () => {
    const user = userEvent.setup();
    server.use(...as(BuiltInRole.Owner));
    renderInTenant(<BillingPage />, { url });
    const field = await screen.findByRole("textbox", { name: "Billing email" });

    await user.clear(field);
    await user.type(field, "accounts");
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(
      await screen.findByText("Enter an email address like name@company.com."),
    ).toBeVisible();
  });

  it("ManagePaymentDetails_Click_OpensStripesPortalInANewTab", async () => {
    const user = userEvent.setup();
    const tab = { location: { href: "" }, close: vi.fn() };
    vi.spyOn(window, "open").mockReturnValue(tab as unknown as Window);
    const sent: unknown[] = [];
    server.use(
      http.post(
        `*/v1/tenants/${northwind.id}/billing/portal-session`,
        async ({ request }) => {
          sent.push(await request.json());
          return HttpResponse.json({ url: "https://billing.stripe.test/p/1" });
        },
      ),
      ...as(BuiltInRole.Owner),
    );
    renderInTenant(<BillingPage />, { url });

    await user.click(
      await screen.findByRole("button", { name: "Manage payment details" }),
    );

    await vi.waitFor(() =>
      expect(tab.location.href).toBe("https://billing.stripe.test/p/1"),
    );
    expect(sent).toEqual([
      { returnUrl: `${window.location.origin}/northwind/settings/billing` },
    ]);
  });

  it("BillingPage_CheckoutCancelled_SaysNothingChangedWithTryAgain", async () => {
    server.use(...as(BuiltInRole.Owner));

    renderInTenant(<BillingPage />, { url: `${url}?checkout=cancelled` });

    expect(
      await screen.findByText("Payment not completed. Nothing changed."),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Try again" })).toHaveAttribute(
      "href",
      "/northwind/settings/billing/plans",
    );
  });

  it("BillingPage_CheckoutSuccess_SaysThePlanAndDropsTheQuery", async () => {
    server.use(...as(BuiltInRole.Owner));

    renderInTenant(<BillingPage />, { url: `${url}?checkout=success` });

    await expectVisibleToast("Payment successful. You’re on Team.");
    await vi.waitFor(() => expect(window.location.search).toBe(""));
  });

  it("CancelPlan_Confirm_MovesToTheDefaultPlanAtThePeriodsEnd", async () => {
    const user = userEvent.setup();
    const sent: unknown[] = [];
    server.use(
      http.post(
        `*/v1/tenants/${northwind.id}/billing/plan-change`,
        async ({ request }) => {
          sent.push(await request.json());
          return HttpResponse.json({
            move: "cancellation",
            at: "2026-12-01T00:00:00Z",
          });
        },
      ),
      ...as(BuiltInRole.Owner),
    );
    renderInTenant(<BillingPage />, { url });

    await user.click(
      await screen.findByRole("button", { name: "Cancel plan" }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Cancel Team?",
    });
    expect(
      within(dialog).getByText("Free gives less than you use today"),
    ).toBeVisible();
    expect(
      within(dialog).getByText(/Seats: Free gives 3, you use 12/),
    ).toBeVisible();
    await user.click(
      within(dialog).getByRole("button", {
        name: "Move to Free on 1 Dec 2026",
      }),
    );

    await expectVisibleToast("Cancellation scheduled");
    expect(sent).toEqual([
      { planId: freePlan.id, interval: "monthly", proratedAt: null },
    ]);
  });
});
