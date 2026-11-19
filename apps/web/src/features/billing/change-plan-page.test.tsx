import { BuiltInRole, ErrorCode } from "@repo/api-client";
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
  businessPlan,
  freePlan,
  teamPlan,
  teamTenant,
} from "../../../test/billing";
import {
  northwind,
  permissionsOf,
  problem,
  renderInTenant,
  tenantAnswers,
} from "../../../test/tenant";
import { ChangePlanPage } from "./change-plan-page";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
  notFound: () => {
    throw new Error("not found");
  },
}));

// clerk-js as loaded: useFreshRouter waits for it, then asks the session for a token before it navigates.
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({
    on: (_: string, handler: (status: string) => void) => handler("ready"),
    off: vi.fn(),
    session: { getToken: async () => "token" },
  }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => {
  server.resetHandlers();
  push.mockReset();
});
afterAll(() => server.close());

const url = "/northwind/settings/billing/plans";

const owner = (billing = billingOf()) => [
  ...billingAnswers(billing),
  ...tenantAnswers(BuiltInRole.Owner, permissionsOf.owner, [], teamTenant),
];

/** The card of a plan in the list. */
const card = async (name: string) =>
  within(
    (await screen.findByRole("heading", { level: 2, name })).closest(
      "li",
    ) as HTMLElement,
  );

const preview = http.post(
  `*/v1/tenants/${northwind.id}/billing/plan-change/preview`,
  () =>
    HttpResponse.json({
      move: "at_once",
      at: "2026-11-15T10:00:00Z",
      currency: "EUR",
      amount: 17900,
      amountToday: 5325,
      proratedAt: "2026-11-15T10:00:00Z",
    }),
);

describe("ChangePlanPage", () => {
  it("ChangePlanPage_TeamMonthly_ListsEachPlanForTheOrganisationsPeople", async () => {
    server.use(...owner());

    renderInTenant(<ChangePlanPage />, { url });

    const team = await card("Team");
    expect(
      team.getByText("For your 10 people: €100 a month, excl. VAT."),
    ).toBeVisible();
    expect(team.getByText("Current plan", { selector: "span" })).toBeVisible();
    expect(team.getByRole("button", { name: "Current plan" })).toBeDisabled();
    const business = await card("Business");
    expect(business.getByText("€99 + €8")).toBeVisible();
    expect(
      business.getByText("For your 10 people: €179 a month, excl. VAT."),
    ).toBeVisible();
    expect(business.getByText("50 seats")).toBeVisible();
    expect(
      business.getByRole("button", { name: "Upgrade to Business" }),
    ).toBeEnabled();
    const free = await card("Free");
    expect(free.getByText("No custom roles")).toBeVisible();
    expect(free.getByRole("button", { name: "Move to Free" })).toBeEnabled();
  });

  it("ChangePlanPage_Yearly_ShowsTheYearsPriceAndTheMonthsItSaves", async () => {
    const user = userEvent.setup();
    server.use(...owner());
    renderInTenant(<ChangePlanPage />, { url });

    await user.click(
      await screen.findByRole("radio", { name: "Yearly · 2 months free" }),
    );

    const team = await card("Team");
    expect(
      team.getByText(
        "For your 10 people: €1,000 a year, excl. VAT. 2 months free.",
      ),
    ).toBeVisible();
    expect(
      team.getByRole("button", { name: "Switch to yearly" }),
    ).toBeEnabled();
  });

  it("Upgrade_Confirm_PaysTheRestOfThePeriodAtThePreviewsMoment", async () => {
    const user = userEvent.setup();
    const sent: unknown[] = [];
    server.use(
      preview,
      http.post(
        `*/v1/tenants/${northwind.id}/billing/plan-change`,
        async ({ request }) => {
          sent.push(await request.json());
          return HttpResponse.json({
            move: "at_once",
            at: "2026-11-15T10:00:00Z",
          });
        },
      ),
      ...owner(),
    );
    renderInTenant(<ChangePlanPage />, { url });

    await user.click(
      (await card("Business")).getByRole("button", {
        name: "Upgrade to Business",
      }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Team → Business",
    });
    expect(
      await within(dialog).findByText("Today, for the rest of November"),
    ).toBeVisible();
    await user.click(
      within(dialog).getByRole("button", { name: "Upgrade and pay €53.25" }),
    );

    await expectVisibleToast("You’re on Business");
    expect(sent).toEqual([
      {
        planId: businessPlan.id,
        interval: "monthly",
        proratedAt: "2026-11-15T10:00:00Z",
      },
    ]);
    expect(push).toHaveBeenCalledWith("/northwind/settings/billing");
  });

  it("Upgrade_CardDeclined_SaysNothingChangedAndOffersAnotherCard", async () => {
    const user = userEvent.setup();
    server.use(
      preview,
      http.post(`*/v1/tenants/${northwind.id}/billing/plan-change`, () =>
        problem(402, ErrorCode.BillingPaymentDeclined),
      ),
      ...owner(),
    );
    renderInTenant(<ChangePlanPage />, { url });
    await user.click(
      (await card("Business")).getByRole("button", {
        name: "Upgrade to Business",
      }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Team → Business",
    });

    await user.click(
      await within(dialog).findByRole("button", {
        name: "Upgrade and pay €53.25",
      }),
    );

    expect(
      await within(dialog).findByText(
        "Your card was declined. Nothing changed.",
      ),
    ).toBeVisible();
    expect(
      within(dialog).getByRole("button", { name: "Try another card" }),
    ).toBeVisible();
  });

  it("MoveToFree_Click_OpensTheCancellation", async () => {
    const user = userEvent.setup();
    server.use(...owner());
    renderInTenant(<ChangePlanPage />, { url });

    await user.click(
      (await card("Free")).getByRole("button", { name: "Move to Free" }),
    );

    expect(
      await screen.findByRole("alertdialog", { name: "Cancel Team?" }),
    ).toBeVisible();
  });

  it("ChangePlanPage_NeverPaid_OpensStripesCheckoutForThePlan", async () => {
    const user = userEvent.setup();
    const sent: unknown[] = [];
    const assign = vi.fn();
    vi.stubGlobal("location", { ...window.location, assign });
    server.use(
      http.post(
        `*/v1/tenants/${northwind.id}/billing/checkout-session`,
        async ({ request }) => {
          sent.push(await request.json());
          return HttpResponse.json({ url: "https://checkout.stripe.test/c/1" });
        },
      ),
      ...billingAnswers(billingOf(null)),
      ...tenantAnswers(BuiltInRole.Owner, permissionsOf.owner, [], {
        ...teamTenant,
        planName: freePlan.name,
      }),
    );
    renderInTenant(<ChangePlanPage />, { url });

    await user.click(
      (await card("Team")).getByRole("button", { name: "Upgrade to Team" }),
    );

    await vi.waitFor(() =>
      expect(assign).toHaveBeenCalledWith("https://checkout.stripe.test/c/1"),
    );
    expect(sent).toEqual([
      {
        planId: teamPlan.id,
        interval: "monthly",
        returnUrl: expect.stringMatching(/\/northwind\/settings\/billing$/),
      },
    ]);
    vi.unstubAllGlobals();
  });
});
