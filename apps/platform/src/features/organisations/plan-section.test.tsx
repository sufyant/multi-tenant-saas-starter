import {
  EntitlementOverrideResponseState,
  ErrorCode,
  FeatureKey,
  LimitKey,
  TenantResponseStatus,
} from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, waitFor, within } from "@testing-library/react";
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
  kestrel,
  meAnswer,
  moyne,
  problem,
} from "../../../test/organisations";
import {
  burrenPlan,
  ciaraOverride,
  free,
  max,
  off,
  plansAnswer,
  plansCommand,
  tenantPlanAnswer,
} from "../../../test/plans";
import { renderWithProviders, sentence } from "../../../test/render";
import { PlanSection } from "./plan-section";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => server.use(meAnswer(), plansAnswer()));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

type Call = { key: string | null; body: unknown; url: URL };

const active = detailOf(burren, { status: TenantResponseStatus.Active });

const renderSection = async (
  organisation = active,
  plan = burrenPlan(),
  reads: { count: number } = { count: 0 },
) => {
  server.use(tenantPlanAnswer({ ...plan, tenantId: organisation.id }, reads));
  renderWithProviders(<PlanSection organisation={organisation} />);
  return within(await screen.findByRole("table", { name: "Plan" }));
};

/** The row of a key in the section's table. */
const rowOf = (table: ReturnType<typeof within>, name: string) =>
  within(
    table.getByText(name, { selector: "tr *" }).closest("tr") as HTMLElement,
  );

const openMenu = async (name: string) => {
  await userEvent.click(
    within(screen.getByRole("table", { name: "Plan" })).getByRole("button", {
      name: `Actions for ${name}`,
    }),
  );
};

describe("PlanSection", () => {
  it("Section_Default_ShowsThePlanAndARowPerKey", async () => {
    const table = await renderSection();

    expect(
      screen.getByRole("heading", { level: 2, name: "Plan" }),
    ).toBeVisible();
    expect(screen.getByText("Pro")).toBeVisible();
    expect(screen.getByRole("button", { name: "Change plan" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Add override" })).toBeEnabled();
    for (const header of [
      "Feature or limit",
      "Plan",
      "Override",
      "Applies",
      "Used",
    ])
      expect(table.getByRole("columnheader", { name: header })).toBeVisible();

    const roles = rowOf(table, "Custom roles");
    expect(roles.getAllByText("On")).toHaveLength(2);
    const limit = rowOf(table, "Custom roles limit");
    expect(limit.getByText("20")).toBeVisible();
    expect(limit.getByText("25", { selector: "strong" })).toBeVisible();
    expect(limit.getByText(sentence("until 31 Dec 2026"))).toBeVisible();
    expect(
      limit.getByText("Moving their old roles over; review at year end."),
    ).toBeVisible();
    expect(limit.getByText("Set by Ciara Byrne")).toBeVisible();
    expect(limit.getByText("14")).toBeVisible();
    const members = rowOf(table, "Members limit");
    expect(members.getAllByText("Unlimited")).toHaveLength(2);
    expect(members.getByText("22")).toBeVisible();
    expect(
      screen.getByText(
        "An override changes one value for this organisation only. The plan stays as it is.",
      ),
    ).toBeVisible();
    expect(screen.queryByText(/is over a limit/)).toBeNull();
  });

  it("Section_NoOverrides_SaysEveryValueComesFromThePlan", async () => {
    await renderSection(
      active,
      burrenPlan({
        [LimitKey.RolesCustomMax]: { override: null, effective: max(20) },
      }),
    );

    expect(
      screen.getByText("No overrides. Every value comes from the plan."),
    ).toBeVisible();
  });

  it("Section_NoExpiry_SaysSo", async () => {
    const table = await renderSection(
      active,
      burrenPlan({
        [LimitKey.RolesCustomMax]: {
          override: ciaraOverride({ expiresAt: null }),
        },
      }),
    );

    expect(
      rowOf(table, "Custom roles limit").getByText("No expiry"),
    ).toBeVisible();
  });

  it("Section_OverALimit_ShowsTheBannerAndOverBy", async () => {
    const table = await renderSection(
      active,
      burrenPlan({
        [LimitKey.RolesCustomMax]: {
          planValue: max(10),
          override: null,
          effective: max(10),
        },
      }),
    );

    expect(
      screen.getByText(sentence("Burren Supply is over a limit.")),
    ).toBeVisible();
    expect(
      screen.getByText(
        "Nothing is deleted or locked. Its people can’t add more until it is under the limit.",
      ),
    ).toBeVisible();
    expect(
      rowOf(table, "Custom roles limit").getByText("Over by 4"),
    ).toBeVisible();
  });

  it("Section_ExpiredOverride_StaysListedAndCrossedOut", async () => {
    const table = await renderSection(
      active,
      burrenPlan({
        [LimitKey.RolesCustomMax]: {
          planValue: max(10),
          override: ciaraOverride({
            expiresAt: "2026-10-20T00:00:00Z",
            state: EntitlementOverrideResponseState.Expired,
          }),
          effective: max(10),
        },
      }),
    );

    const limit = rowOf(table, "Custom roles limit");
    expect(limit.getByText("Expired")).toBeVisible();
    expect(limit.getByText("25").closest("del")).not.toBeNull();
    expect(limit.getByText("10", { selector: "strong" })).toBeVisible();
    expect(limit.getByText("Over by 4")).toBeVisible();
  });

  it("Section_Closed_IsReadOnlyWithItsBanner", async () => {
    await renderSection(detailOf(kestrel));

    expect(
      screen.getByText(
        sentence(
          "Kestrel Labs is closed. Its plan and overrides are read-only.",
        ),
      ),
    ).toBeVisible();
    expect(screen.getByText("Reopen it to change them.")).toBeVisible();
    expect(screen.queryByRole("button", { name: "Change plan" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add override" })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Actions for/ })).toBeNull();
  });

  it("Section_Purging_IsReadOnly", async () => {
    await renderSection(detailOf(moyne));

    expect(screen.queryByRole("button", { name: "Change plan" })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Actions for/ })).toBeNull();
  });

  it("Section_Suspended_CanChange", async () => {
    await renderSection(
      detailOf(burren, { status: TenantResponseStatus.Suspended }),
    );

    expect(screen.getByRole("button", { name: "Change plan" })).toBeEnabled();
  });

  it("Section_Loading_ShowsTheSkeleton", async () => {
    server.use(
      http.get(
        `*/v1/platform/tenants/${burren.id}/plan`,
        () => new Promise(() => {}),
      ),
    );

    renderWithProviders(<PlanSection organisation={active} />);

    expect(await screen.findByRole("status", { name: "Plan" })).toBeVisible();
  });

  it("Section_Error_ShowsItAndTriesAgain", async () => {
    let calls = 0;
    server.use(
      http.get(`*/v1/platform/tenants/${burren.id}/plan`, () => {
        calls += 1;
        return calls === 1
          ? problem(500, ErrorCode.ServerError)
          : HttpResponse.json(burrenPlan());
      }),
    );

    renderWithProviders(<PlanSection organisation={active} />);

    expect(await screen.findByText("Couldn’t load the plan")).toBeVisible();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByRole("table", { name: "Plan" })).toBeVisible();
  });

  it("RowMenu_WithAnOverride_EditsOrRemoves", async () => {
    await renderSection();

    await openMenu("Custom roles limit");

    expect(
      await screen.findByRole("menuitem", { name: "Edit override" }),
    ).toBeVisible();
    expect(
      screen.getByRole("menuitem", { name: "Remove override" }),
    ).toBeVisible();
  });

  it("RowMenu_WithoutAnOverride_Adds", async () => {
    await renderSection();

    await openMenu("Members limit");

    expect(
      await screen.findByRole("menuitem", { name: "Add override" }),
    ).toBeVisible();
  });

  it("AddOverride_Saved_SendsValueExpiryAndReason", async () => {
    const calls: Call[] = [];
    const reads = { count: 0 };
    await renderSection(active, burrenPlan(), reads);
    server.use(
      plansCommand(
        "put",
        `tenants/${burren.id}/entitlement-overrides/${LimitKey.MembersMax}`,
        [() => HttpResponse.json(burrenPlan())],
        calls,
      ),
    );

    await openMenu("Members limit");
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Add override" }),
    );
    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Add an override for Burren Supply",
      }),
    );
    expect(
      dialog.getByText(
        sentence(
          "It changes this value for Burren Supply only, until it expires or someone removes it.",
        ),
      ),
    ).toBeVisible();
    expect(
      dialog.getByRole("combobox", { name: "Feature or limit" }),
    ).toHaveTextContent("Members limit");
    // It starts from the value that applies: Members limit is unlimited.
    expect(dialog.getByRole("checkbox", { name: "Unlimited" })).toBeChecked();
    await userEvent.click(dialog.getByRole("checkbox", { name: "Unlimited" }));
    await userEvent.type(
      dialog.getByRole("spinbutton", { name: "Value" }),
      "40",
    );
    await userEvent.type(dialog.getByLabelText("Expires"), "2026-11-30");
    await userEvent.type(
      dialog.getByRole("textbox", { name: "Reason" }),
      "Trial of the bigger team",
    );
    await userEvent.click(
      dialog.getByRole("button", { name: "Save override" }),
    );

    await waitFor(() => expect(calls).toHaveLength(1));
    expect(calls[0]?.body).toEqual({
      value: { maximum: 40 },
      expiresAt: "2026-11-30T00:00:00.000Z",
      reason: "Trial of the bigger team",
    });
    await expectVisibleToast("Override saved");
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("AddOverride_FromTheHeader_ChoosesTheKey", async () => {
    const calls: Call[] = [];
    await renderSection();
    server.use(
      plansCommand(
        "put",
        `tenants/${burren.id}/entitlement-overrides/${FeatureKey.RolesCustom}`,
        [() => HttpResponse.json(burrenPlan())],
        calls,
      ),
    );

    await userEvent.click(screen.getByRole("button", { name: "Add override" }));
    const dialog = within(await screen.findByRole("alertdialog"));
    await userEvent.click(
      dialog.getByRole("combobox", { name: "Feature or limit" }),
    );
    await userEvent.click(
      await screen.findByRole("option", { name: "Custom roles" }),
    );
    await userEvent.click(dialog.getByRole("switch", { name: "Value" }));
    await userEvent.type(
      dialog.getByRole("textbox", { name: "Reason" }),
      "Off for review",
    );
    await userEvent.click(
      dialog.getByRole("button", { name: "Save override" }),
    );

    await waitFor(() => expect(calls).toHaveLength(1));
    expect(calls[0]?.body).toEqual({
      value: { enabled: false },
      expiresAt: null,
      reason: "Off for review",
    });
  });

  it("AddOverride_NoReason_SendsNothingAndAsksForOne", async () => {
    const calls: Call[] = [];
    await renderSection();
    server.use(
      plansCommand(
        "put",
        `tenants/${burren.id}/entitlement-overrides/${LimitKey.MembersMax}`,
        [() => HttpResponse.json(burrenPlan())],
        calls,
      ),
    );

    await openMenu("Members limit");
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Add override" }),
    );
    const dialog = within(await screen.findByRole("alertdialog"));
    await userEvent.click(
      dialog.getByRole("button", { name: "Save override" }),
    );

    expect(await dialog.findByText("Enter a reason.")).toBeVisible();
    expect(calls).toEqual([]);
  });

  it("EditOverride_StartsFromTheOverride", async () => {
    await renderSection();

    await openMenu("Custom roles limit");
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Edit override" }),
    );

    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Edit the override on Custom roles limit",
      }),
    );
    expect(dialog.getByRole("spinbutton", { name: "Value" })).toHaveValue(25);
    expect(dialog.getByLabelText("Expires")).toHaveValue("2026-12-31");
    expect(dialog.getByRole("textbox", { name: "Reason" })).toHaveValue(
      "Moving their old roles over; review at year end.",
    );
    expect(
      dialog.queryByRole("combobox", { name: "Feature or limit" }),
    ).toBeNull();
  });

  it("SetOverride_NotActive_ShowsItsTextAndReadsAgain", async () => {
    const reads = { count: 0 };
    await renderSection(active, burrenPlan(), reads);
    server.use(
      plansCommand(
        "put",
        `tenants/${burren.id}/entitlement-overrides/${LimitKey.RolesCustomMax}`,
        [() => problem(409, ErrorCode.TenantNotActive)],
      ),
    );

    await openMenu("Custom roles limit");
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Edit override" }),
    );
    const dialog = within(await screen.findByRole("alertdialog"));
    await userEvent.click(
      dialog.getByRole("button", { name: "Save override" }),
    );

    expect(
      await dialog.findByText(
        sentence(
          "Burren Supply is closed. Its plan and overrides are read-only.",
        ),
      ),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("SetOverride_ReasonRefused_ShowsItUnderTheReason", async () => {
    await renderSection();
    server.use(
      plansCommand(
        "put",
        `tenants/${burren.id}/entitlement-overrides/${LimitKey.RolesCustomMax}`,
        [
          () =>
            problem(400, ErrorCode.RequestValidationFailed, {
              reason: ["required"],
            }),
        ],
      ),
    );

    await openMenu("Custom roles limit");
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Edit override" }),
    );
    const dialog = within(await screen.findByRole("alertdialog"));
    await userEvent.click(
      dialog.getByRole("button", { name: "Save override" }),
    );

    expect(await dialog.findByText("Enter a reason.")).toBeVisible();
  });

  it("RemoveOverride_Confirmed_DeletesAndReadsAgain", async () => {
    const calls: Call[] = [];
    const reads = { count: 0 };
    await renderSection(active, burrenPlan(), reads);
    server.use(
      plansCommand(
        "delete",
        `tenants/${burren.id}/entitlement-overrides/${LimitKey.RolesCustomMax}`,
        [() => new HttpResponse(null, { status: 204 })],
        calls,
      ),
    );

    await openMenu("Custom roles limit");
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Remove override" }),
    );
    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Remove the override on Custom roles limit?",
      }),
    );
    expect(
      dialog.getByText(
        sentence(
          "Burren Supply goes back to the plan’s value, 20. It has 14 now.",
        ),
      ),
    ).toBeVisible();
    await userEvent.click(
      dialog.getByRole("button", { name: "Remove override" }),
    );

    await waitFor(() => expect(calls).toHaveLength(1));
    await expectVisibleToast("Override removed");
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("RemoveOverride_AlreadyGone_ShowsItsTextAndReadsAgain", async () => {
    const reads = { count: 0 };
    await renderSection(active, burrenPlan(), reads);
    server.use(
      plansCommand(
        "delete",
        `tenants/${burren.id}/entitlement-overrides/${LimitKey.RolesCustomMax}`,
        [() => problem(404, ErrorCode.PlanOverrideNotFound)],
      ),
    );

    await openMenu("Custom roles limit");
    await userEvent.click(
      await screen.findByRole("menuitem", { name: "Remove override" }),
    );
    const dialog = within(await screen.findByRole("alertdialog"));
    await userEvent.click(
      dialog.getByRole("button", { name: "Remove override" }),
    );

    expect(
      await dialog.findByText("This organisation has no override of this key."),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("ChangePlan_Preview_ListsWhatTheNewPlanChanges", async () => {
    const previews: Call[] = [];
    await renderSection();
    server.use(
      plansCommand(
        "post",
        `tenants/${burren.id}/plan/preview`,
        [
          () =>
            HttpResponse.json({
              excesses: [
                {
                  key: FeatureKey.RolesCustom,
                  count: 14,
                  newValue: off,
                  amount: 14,
                },
                {
                  key: LimitKey.MembersMax,
                  count: 22,
                  newValue: max(5),
                  amount: 17,
                },
              ],
              overriddenKeys: [LimitKey.RolesCustomMax],
            }),
        ],
        previews,
      ),
    );

    await userEvent.click(screen.getByRole("button", { name: "Change plan" }));
    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Change Burren Supply’s plan",
      }),
    );
    await userEvent.click(dialog.getByRole("combobox", { name: "New plan" }));
    expect(
      await screen.findByRole("option", { name: /Free.*Default/ }),
    ).toBeVisible();
    expect(screen.getByRole("option", { name: /Pro.*Current/ })).toBeVisible();
    await userEvent.click(screen.getByRole("option", { name: /Free/ }));

    expect(
      await dialog.findByText(
        sentence(
          "On Free, Burren Supply goes over a limit and loses a feature. Nothing is deleted or locked: its people can’t add more until it is under the limit.",
        ),
      ),
    ).toBeVisible();
    expect(
      dialog.getByText(
        "Custom roles turns off. Its 14 custom roles stay, and their members keep their permissions; nobody can create, edit or give one.",
      ),
    ).toBeVisible();
    expect(
      dialog.getByText(sentence("Members limit: 22 now, 5 on Free.")),
    ).toBeVisible();
    expect(dialog.getByText("Over by 17")).toBeVisible();
    expect(
      dialog.getByText("Custom roles limit: its override (25) still applies."),
    ).toBeVisible();
    expect(previews[0]?.body).toEqual({ planId: free.id });
  });

  it("ChangePlan_Confirmed_PutsThePlanAndReadsAgain", async () => {
    const calls: Call[] = [];
    const reads = { count: 0 };
    await renderSection(active, burrenPlan(), reads);
    server.use(
      plansCommand("post", `tenants/${burren.id}/plan/preview`, [
        () => HttpResponse.json({ excesses: [], overriddenKeys: [] }),
      ]),
      plansCommand(
        "put",
        `tenants/${burren.id}/plan`,
        [() => HttpResponse.json(burrenPlan())],
        calls,
      ),
    );

    await userEvent.click(screen.getByRole("button", { name: "Change plan" }));
    const dialog = within(await screen.findByRole("alertdialog"));
    await userEvent.click(dialog.getByRole("combobox", { name: "New plan" }));
    await userEvent.click(await screen.findByRole("option", { name: /Free/ }));
    await userEvent.click(dialog.getByRole("button", { name: "Change plan" }));

    await waitFor(() => expect(calls).toHaveLength(1));
    expect(calls[0]?.body).toEqual({ planId: free.id });
    await expectVisibleToast(sentence("Burren Supply moved to Free"));
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("ChangePlan_PlanGone_ShowsItsTextAndReadsThePlansAgain", async () => {
    const plansRequests: URLSearchParams[] = [];
    await renderSection();
    server.use(
      plansAnswer(undefined, plansRequests),
      plansCommand("post", `tenants/${burren.id}/plan/preview`, [
        () => HttpResponse.json({ excesses: [], overriddenKeys: [] }),
      ]),
      plansCommand("put", `tenants/${burren.id}/plan`, [
        () => problem(404, ErrorCode.PlanNotFound),
      ]),
    );

    await userEvent.click(screen.getByRole("button", { name: "Change plan" }));
    const dialog = within(await screen.findByRole("alertdialog"));
    await userEvent.click(dialog.getByRole("combobox", { name: "New plan" }));
    await userEvent.click(await screen.findByRole("option", { name: /Free/ }));
    await userEvent.click(dialog.getByRole("button", { name: "Change plan" }));

    expect(
      await dialog.findByText("This plan no longer exists."),
    ).toBeVisible();
    await waitFor(() => expect(plansRequests.length).toBeGreaterThan(0));
  });
});
