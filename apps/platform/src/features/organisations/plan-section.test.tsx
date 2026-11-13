import {
  EntitlementOverrideResponseState,
  ErrorCode,
  FeatureKey,
  LimitKey,
  TenantResponseStatus,
} from "@repo/api-client";
import { expectShownReason } from "@repo/ui/test/dialog";
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
  unlimited,
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
  await screen.findByRole("group", { name: "Plan" });
};

/** Burren Supply with Custom roles off by an override (4.50): Pro gives it, the override turns it off. */
const featureOffPlan = (rows: Parameters<typeof burrenPlan>[0] = {}) =>
  burrenPlan({
    [FeatureKey.RolesCustom]: {
      override: ciaraOverride({
        value: off,
        expiresAt: null,
        reason: "Contract paused",
      }),
      effective: off,
    },
    ...rows,
  });

/** The row of a key in the section's card: a group named by the key. */
const rowOf = (name: string) => within(screen.getByRole("group", { name }));

const editOverride = (name: string) =>
  userEvent.click(
    rowOf(name).getByRole("button", { name: `Edit the override on ${name}` }),
  );

const removeOverride = (name: string) =>
  userEvent.click(
    rowOf(name).getByRole("button", {
      name: `Remove the override on ${name}`,
    }),
  );

describe("PlanSection", () => {
  it("Section_Default_ShowsThePlanAndARowPerKey", async () => {
    await renderSection(
      detailOf(burren, {
        status: TenantResponseStatus.Active,
        memberCount: 20,
        pendingInvitationCount: 2,
      }),
    );

    expect(rowOf("Plan").getByText("Pro")).toBeVisible();
    expect(screen.getByRole("button", { name: "Change plan" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Add override" })).toBeEnabled();
    expect(rowOf("Custom roles").getByText("On")).toBeVisible();
    expect(
      rowOf("Custom roles").getByText(sentence("Pro gives On.")),
    ).toBeVisible();
    const limit = rowOf("Custom roles limit");
    expect(limit.getByText("14 of 25")).toBeVisible();
    expect(
      limit.getByText(
        sentence(
          "Pro gives 20. Override: 25 until 31 Dec 2026 · Reason: Moving their old roles over; review at year end. · by Ciara Byrne",
        ),
      ),
    ).toBeVisible();
    const seats = rowOf("Seats");
    expect(seats.getByText("Unlimited")).toBeVisible();
    expect(
      seats.getByText("20 members and 2 pending invitations"),
    ).toBeVisible();
    expect(screen.queryByText(/is over a limit/)).toBeNull();
  });

  it("Section_Keys_FollowTheAreasOfTheFrames", async () => {
    await renderSection();

    expect(
      screen
        .getAllByRole("group")
        .map((group) => group.getAttribute("aria-labelledby"))
        .map((id) => document.getElementById(id ?? "")?.textContent),
    ).toEqual([
      "Plan",
      "Seats",
      "Custom roles",
      "Custom roles limit",
      "Activity log",
    ]);
  });

  // 4.14: the period in days, with what the plan gives; it counts nothing, so it has no "of".
  it("Section_ActivityLog_ShowsItsDaysAndWhatThePlanGives", async () => {
    await renderSection();

    expect(rowOf("Activity log").getByText("365 days")).toBeVisible();
    expect(
      rowOf("Activity log").getByText(sentence("Pro gives 365 days.")),
    ).toBeVisible();
  });

  it("Section_LimitedSeats_SaysTheSeatsInTheMembersSentence", async () => {
    await renderSection(
      detailOf(burren, {
        status: TenantResponseStatus.Active,
        memberCount: 10,
        pendingInvitationCount: 2,
      }),
      burrenPlan({
        [LimitKey.MembersMax]: {
          planValue: max(15),
          effective: max(15),
          count: 12,
        },
      }),
    );

    const seats = rowOf("Seats");
    expect(seats.getByText("12 of 15")).toBeVisible();
    expect(
      seats.getByText("10 members and 2 pending invitations: 12 of 15 seats."),
    ).toBeVisible();
  });

  it("Section_NoExpiry_SaysNoDate", async () => {
    await renderSection(
      active,
      burrenPlan({
        [LimitKey.RolesCustomMax]: {
          override: ciaraOverride({ expiresAt: null }),
        },
      }),
    );

    expect(
      rowOf("Custom roles limit").getByText(
        sentence(
          "Pro gives 20. Override: 25 · Reason: Moving their old roles over; review at year end. · by Ciara Byrne",
        ),
      ),
    ).toBeVisible();
  });

  it("Section_NoOverride_HasNoEditOrRemove", async () => {
    await renderSection(
      active,
      burrenPlan({
        [LimitKey.RolesCustomMax]: { override: null, effective: max(20) },
      }),
    );

    expect(
      rowOf("Custom roles limit").getByText(sentence("Pro gives 20.")),
    ).toBeVisible();
    expect(
      screen.queryByRole("button", { name: /^Edit the override/ }),
    ).toBeNull();
  });

  it("Section_OverALimit_ShowsTheBannerAndOverBy", async () => {
    await renderSection(
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
    expect(rowOf("Custom roles limit").getByText("Over by 4")).toBeVisible();
  });

  it("Section_ExpiredOverride_StaysListedAndCrossedOut", async () => {
    await renderSection(
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

    const limit = rowOf("Custom roles limit");
    expect(limit.getByText("Expired")).toBeVisible();
    expect(limit.getByText("25").closest("del")).not.toBeNull();
    expect(limit.getByText("14 of 10")).toBeVisible();
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
    expect(
      screen.queryByRole("button", { name: /^Edit the override/ }),
    ).toBeNull();
  });

  it("Section_Purging_IsReadOnly", async () => {
    await renderSection(detailOf(moyne));

    expect(screen.queryByRole("button", { name: "Change plan" })).toBeNull();
    expect(
      screen.queryByRole("button", { name: /^Remove the override/ }),
    ).toBeNull();
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
    expect(await screen.findByRole("group", { name: "Plan" })).toBeVisible();
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

    await userEvent.click(screen.getByRole("button", { name: "Add override" }));
    await userEvent.click(
      within(await screen.findByRole("alertdialog")).getByRole("combobox", {
        name: "Feature or limit",
      }),
    );
    await userEvent.click(await screen.findByRole("option", { name: "Seats" }));
    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Add an override" }),
    );
    expect(
      dialog.getByText(
        sentence(
          "An override changes one value for Burren Supply only. The plan stays the same.",
        ),
      ),
    ).toBeVisible();
    expect(
      dialog.getByRole("combobox", { name: "Feature or limit" }),
    ).toHaveTextContent("Seats");
    // It starts from the value that applies: Seats is unlimited.
    expect(dialog.getByRole("checkbox", { name: "Unlimited" })).toBeChecked();
    await userEvent.click(dialog.getByRole("checkbox", { name: "Unlimited" }));
    await userEvent.type(
      dialog.getByRole("spinbutton", { name: "Value" }),
      "40",
    );
    await userEvent.type(dialog.getByLabelText("Until"), "2026-11-30");
    await userEvent.type(
      dialog.getByRole("textbox", { name: "Reason" }),
      "Trial of the bigger team",
    );
    await userEvent.click(dialog.getByRole("button", { name: "Add override" }));

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
    await userEvent.click(dialog.getByRole("button", { name: "Add override" }));

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

    await userEvent.click(screen.getByRole("button", { name: "Add override" }));
    await userEvent.click(
      within(await screen.findByRole("alertdialog")).getByRole("combobox", {
        name: "Feature or limit",
      }),
    );
    await userEvent.click(await screen.findByRole("option", { name: "Seats" }));
    const dialog = within(await screen.findByRole("alertdialog"));
    await userEvent.click(dialog.getByRole("button", { name: "Add override" }));

    expect(await dialog.findByText("Enter a reason.")).toBeVisible();
    expect(calls).toEqual([]);
  });

  it("EditOverride_StartsFromTheOverride", async () => {
    await renderSection();

    await editOverride("Custom roles limit");

    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Edit the override on Custom roles limit",
      }),
    );
    expect(dialog.getByRole("spinbutton", { name: "Value" })).toHaveValue(25);
    expect(dialog.getByLabelText("Until")).toHaveValue("2026-12-31");
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

    await editOverride("Custom roles limit");
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

    await editOverride("Custom roles limit");
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

    await removeOverride("Custom roles limit");
    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Remove the override on Custom roles limit?",
      }),
    );
    expect(
      dialog.getByText(
        sentence("Burren Supply goes back to Pro’s value: 20. It has 14."),
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

    await removeOverride("Custom roles limit");
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
        name: "Change the plan of Burren Supply",
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
        sentence("From Pro to Free. Here is what changes:"),
      ),
    ).toBeVisible();
    expect(
      dialog.getByText(
        sentence(
          "Custom roles: off on Free. The 14 custom roles keep working for their members; new ones can’t be created.",
        ),
      ),
    ).toBeVisible();
    expect(
      dialog.getByText(
        sentence("Seats: Unlimited → 5. Burren Supply uses 22."),
      ),
    ).toBeVisible();
    expect(
      dialog.getByText(
        "The override on Custom roles limit stays until 31 Dec 2026.",
      ),
    ).toBeVisible();
    expect(previews[0]?.body).toEqual({ planId: free.id });
  });

  it("ChangePlan_CurrentPlanChosen_SaysToChooseAnother", async () => {
    await renderSection();

    await userEvent.click(screen.getByRole("button", { name: "Change plan" }));
    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Change the plan of Burren Supply",
      }),
    );

    expectShownReason(
      dialog.getByRole("button", { name: "Change plan" }),
      "This is the current plan. Choose another.",
    );
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
    await userEvent.click(
      await dialog.findByRole("button", { name: "Change to Free" }),
    );

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
    await userEvent.click(
      await dialog.findByRole("button", { name: "Change to Free" }),
    );

    expect(
      await dialog.findByText("This plan no longer exists."),
    ).toBeVisible();
    await waitFor(() => expect(plansRequests.length).toBeGreaterThan(0));
  });
  /** Opens Add override on the Activity log and types a value, and a day when one is given. */
  const overrideActivityLog = async (value: string, until?: string) => {
    await userEvent.click(screen.getByRole("button", { name: "Add override" }));
    await userEvent.click(
      within(await screen.findByRole("alertdialog")).getByRole("combobox", {
        name: "Feature or limit",
      }),
    );
    await userEvent.click(
      await screen.findByRole("option", { name: "Activity log" }),
    );
    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Add an override" }),
    );
    const field = dialog.getByRole("spinbutton", { name: "Value" });
    await userEvent.clear(field);
    await userEvent.type(field, value);
    if (until) await userEvent.type(dialog.getByLabelText("Until"), until);
    return dialog;
  };

  // 4.16: the floor under Value, and a lower value says which entries stay 30 days.
  it("AddOverride_ActivityLogShorter_SaysTheFloorAndTheHold", async () => {
    await renderSection();

    const dialog = await overrideActivityLog("60", "2027-01-31");

    expect(
      dialog.getByText(sentence("Days, 30 or more. Pro gives 365 days.")),
    ).toBeVisible();
    expect(
      dialog.getByText(
        sentence(
          "This is shorter than Pro’s 365 days. Entries older than 60 days stay 30 days, then go.",
        ),
      ),
    ).toBeVisible();
    expect(dialog.queryByText(/goes back/)).toBeNull();
  });

  // A plan with no limit keeps every entry, so any number is shorter: the sentence names no period of the plan.
  it("AddOverride_ActivityLogOfAPlanWithNoLimit_SaysThePlanKeepsEveryEntry", async () => {
    await renderSection(
      active,
      burrenPlan({
        [LimitKey.AuditRetentionDays]: {
          planValue: unlimited,
          effective: unlimited,
        },
      }),
    );

    await userEvent.click(screen.getByRole("button", { name: "Add override" }));
    await userEvent.click(
      within(await screen.findByRole("alertdialog")).getByRole("combobox", {
        name: "Feature or limit",
      }),
    );
    await userEvent.click(
      await screen.findByRole("option", { name: "Activity log" }),
    );
    const dialog = within(await screen.findByRole("alertdialog"));
    // It starts from the value that applies: unlimited.
    await userEvent.click(dialog.getByRole("checkbox", { name: "Unlimited" }));
    await userEvent.type(
      dialog.getByRole("spinbutton", { name: "Value" }),
      "60",
    );

    expect(
      dialog.getByText(
        sentence(
          "Pro keeps entries with no time limit. Entries older than 60 days stay 30 days, then go.",
        ),
      ),
    ).toBeVisible();
  });

  // 4.49: a longer value until a day shortens the period on that day.
  it("AddOverride_ActivityLogLongerUntilADay_SaysWhenItGoesBack", async () => {
    await renderSection();

    const dialog = await overrideActivityLog("730", "2027-01-31");

    expect(
      dialog.getByText(
        sentence(
          "On 31 Jan 2027 the period goes back to Pro’s 365 days. Then entries older than 365 days stay 30 days, then go.",
        ),
      ),
    ).toBeVisible();
    expect(dialog.queryByText(/is shorter than/)).toBeNull();
  });

  it("AddOverride_ActivityLogLongerWithNoDay_SaysNoHold", async () => {
    await renderSection();

    const dialog = await overrideActivityLog("730");

    expect(dialog.queryByText(/stay 30 days/)).toBeNull();
  });

  it("AddOverride_ActivityLogUnderTheFloor_SendsNothingAndShowsTheFloor", async () => {
    const calls: Call[] = [];
    await renderSection();
    server.use(
      plansCommand(
        "put",
        `tenants/${burren.id}/entitlement-overrides/${LimitKey.AuditRetentionDays}`,
        [() => HttpResponse.json(burrenPlan())],
        calls,
      ),
    );

    const dialog = await overrideActivityLog("7");
    await userEvent.type(
      dialog.getByRole("textbox", { name: "Reason" }),
      "Data minimisation request",
    );
    await userEvent.click(dialog.getByRole("button", { name: "Add override" }));

    expect(await dialog.findByText("Enter 30 days or more.")).toBeVisible();
    expect(calls).toEqual([]);
  });

  /** Burren Supply with an override of the Activity log: the value that applies is the override's. */
  const withRetentionOverride = (days: number) =>
    burrenPlan({
      [LimitKey.AuditRetentionDays]: {
        override: ciaraOverride({ value: max(days), expiresAt: null }),
        effective: max(days),
      },
    });

  // 4.17: removing a longer override shortens the period back to the plan's.
  it("RemoveOverride_ActivityLogLonger_SaysTheHold", async () => {
    await renderSection(active, withRetentionOverride(730));

    await removeOverride("Activity log");

    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Remove the override on Activity log?",
      }),
    );
    expect(
      dialog.getByText(
        sentence(
          "Burren Supply goes back to Pro’s value: 365 days. Its entries older than 365 days stay 30 days, then go.",
        ),
      ),
    ).toBeVisible();
  });

  it("RemoveOverride_ActivityLogShorter_SaysNoHold", async () => {
    await renderSection(active, withRetentionOverride(60));

    await removeOverride("Activity log");

    expect(
      await screen.findByText(
        sentence("Burren Supply goes back to Pro’s value: 365 days."),
      ),
    ).toBeVisible();
  });

  /** Opens Change plan, chooses Free and waits for its preview. */
  const previewFree = async (retentionShortened: unknown) => {
    await renderSection();
    server.use(
      plansCommand("post", `tenants/${burren.id}/plan/preview`, [
        () =>
          HttpResponse.json({
            excesses: [],
            overriddenKeys: [],
            retentionShortened,
          }),
      ]),
    );
    await userEvent.click(screen.getByRole("button", { name: "Change plan" }));
    const dialog = within(await screen.findByRole("alertdialog"));
    await userEvent.click(dialog.getByRole("combobox", { name: "New plan" }));
    await userEvent.click(await screen.findByRole("option", { name: /Free/ }));
    return dialog;
  };

  // 4.15: a move that shortens the period says from and to, and which entries stay 30 days.
  it("ChangePlan_ShortensTheActivityLog_SaysTheHold", async () => {
    const dialog = await previewFree({ fromDays: 365, toDays: 90 });

    expect(
      await dialog.findByText(
        sentence("From Pro to Free. Here is what changes:"),
      ),
    ).toBeVisible();
    expect(
      dialog.getByText(
        "Activity log: 365 → 90 days. Entries older than 90 days stay 30 days, then go.",
      ),
    ).toBeVisible();
  });

  it("ChangePlan_ShortensFromNoLimit_SaysNoTimeLimit", async () => {
    const dialog = await previewFree({ fromDays: null, toDays: 90 });

    expect(
      await dialog.findByText(
        "Activity log: no time limit → 90 days. Entries older than 90 days stay 30 days, then go.",
      ),
    ).toBeVisible();
  });

  it("ChangePlan_NoShortening_SaysNothingOfTheActivityLog", async () => {
    const dialog = await previewFree(null);

    expect(
      await dialog.findByText(sentence("From Pro to Free.")),
    ).toBeVisible();
    expect(dialog.queryByText(/Activity log/)).toBeNull();
  });

  // 37d (4.50): Custom roles off by an override; its limit says Off, keeps the plan's value and the override, and only Remove works.
  it("Section_DependentLimitWithItsFeatureOff_SaysOffAndAllowsOnlyRemove", async () => {
    await renderSection(active, featureOffPlan());

    const limit = rowOf("Custom roles limit");
    expect(limit.getByText("Off")).toBeVisible();
    expect(limit.queryByText("14 of 25")).toBeNull();
    expect(
      limit.getByText(
        sentence(
          "Pro gives 20. Override: 25 until 31 Dec 2026 · Reason: Moving their old roles over; review at year end. · by Ciara Byrne",
        ),
      ),
    ).toBeVisible();
    expect(limit.getByText("Turn on Custom roles first.")).toBeVisible();
    expect(
      limit.getByRole("button", {
        name: "Edit the override on Custom roles limit",
      }),
    ).toBeDisabled();
    expect(
      limit.getByRole("button", {
        name: "Remove the override on Custom roles limit",
      }),
    ).toBeEnabled();
    expect(rowOf("Custom roles").getByText("Off")).toBeVisible();
    expect(
      rowOf("Custom roles").getByRole("button", {
        name: "Edit the override on Custom roles",
      }),
    ).toBeEnabled();
  });

  // 37d: a limit whose feature is off applies nothing yet, so a count above it is over nothing.
  it("Section_DependentLimitWithItsFeatureOffAndACountAboveIt_IsOverNothing", async () => {
    await renderSection(
      active,
      featureOffPlan({ [LimitKey.RolesCustomMax]: { count: 30 } }),
    );

    expect(rowOf("Custom roles limit").getByText("Off")).toBeVisible();
    expect(screen.queryByText(/Over by/)).toBeNull();
    expect(screen.queryByText(/is over a limit/)).toBeNull();
  });

  // 37d (4.51): Add override of the limit while the organisation's feature is off: the value and the action are disabled.
  it("AddOverride_DependentLimitWithItsFeatureOff_DisablesTheValueAndTheAction", async () => {
    await renderSection(
      active,
      featureOffPlan({
        [LimitKey.RolesCustomMax]: { override: null, effective: max(20) },
      }),
    );

    await userEvent.click(screen.getByRole("button", { name: "Add override" }));
    const dialog = within(await screen.findByRole("alertdialog"));
    await userEvent.click(
      dialog.getByRole("combobox", { name: "Feature or limit" }),
    );
    await userEvent.click(
      await screen.findByRole("option", { name: "Custom roles limit" }),
    );

    const field = dialog.getByRole("spinbutton", { name: "Value" });
    expect(field).toBeDisabled();
    expect(field).toHaveValue(20);
    expect(field).toHaveAccessibleDescription("Turn on Custom roles first.");
    expect(dialog.getByRole("button", { name: "Add override" })).toBeDisabled();
    await userEvent.click(
      dialog.getByRole("combobox", { name: "Feature or limit" }),
    );
    await userEvent.click(await screen.findByRole("option", { name: "Seats" }));
    expect(dialog.getByRole("button", { name: "Add override" })).toBeEnabled();
  });

  it("AddOverride_DependentLimitWithItsFeatureOff_DescribesTheActionWithTheNote", async () => {
    await renderSection(
      active,
      featureOffPlan({
        [LimitKey.RolesCustomMax]: { override: null, effective: max(20) },
      }),
    );

    await userEvent.click(screen.getByRole("button", { name: "Add override" }));
    const dialog = within(await screen.findByRole("alertdialog"));
    await userEvent.click(
      dialog.getByRole("combobox", { name: "Feature or limit" }),
    );
    await userEvent.click(
      await screen.findByRole("option", { name: "Custom roles limit" }),
    );

    expectShownReason(
      dialog.getByRole("button", { name: "Add override" }),
      "Turn on Custom roles first.",
    );
  });

  // 37d (4.52): the removal stays allowed, and says when the plan's value applies.
  it("RemoveOverride_DependentLimitWithItsFeatureOff_SaysThePlansValueAppliesWhenTheFeatureTurnsOn", async () => {
    const calls: Call[] = [];
    await renderSection(active, featureOffPlan());
    server.use(
      plansCommand(
        "delete",
        `tenants/${burren.id}/entitlement-overrides/${LimitKey.RolesCustomMax}`,
        [() => new HttpResponse(null, { status: 204 })],
        calls,
      ),
    );

    await removeOverride("Custom roles limit");
    const dialog = within(
      await screen.findByRole("alertdialog", {
        name: "Remove the override on Custom roles limit?",
      }),
    );
    expect(
      dialog.getByText(
        sentence(
          "Burren Supply goes back to Pro’s value: 20. It applies when Custom roles turns on.",
        ),
      ),
    ).toBeVisible();
    await userEvent.click(
      dialog.getByRole("button", { name: "Remove override" }),
    );

    await waitFor(() => expect(calls).toHaveLength(1));
  });
});
