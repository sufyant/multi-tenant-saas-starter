import {
  ErrorCode,
  FeatureKey,
  FieldErrorCode,
  LimitKey,
} from "@repo/api-client";
import { expectVisibleToast } from "@repo/ui/test/toast";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse } from "msw";
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
import { meAnswer, problem } from "../../../test/organisations";
import {
  excess,
  free,
  partner,
  planAnswer,
  plansAnswer,
  plansCommand,
  plansPage,
  previewPage,
  pro,
} from "../../../test/plans";
import { renderWithProviders, sentence } from "../../../test/render";
import { PlanEditor } from "./plan-editor";

const router = vi.hoisted(() => ({ push: vi.fn() }));
vi.mock("next/navigation", () => ({
  useRouter: () => router,
  usePathname: () => "/plans",
}));
// clerk-js as loaded: useFreshRouter waits for it, then asks the session for a token before it navigates.
const getToken = vi.hoisted(() => vi.fn(async () => "token"));
vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({
    on: (_: string, handler: (status: string) => void) => handler("ready"),
    off: vi.fn(),
    session: { getToken },
  }),
}));
afterEach(() => getToken.mockClear());

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => server.use(meAnswer(), plansAnswer()));
afterEach(() => {
  server.resetHandlers();
  router.push.mockReset();
});
afterAll(() => server.close());

type Call = { key: string | null; body: unknown; url: URL };

const updated = () => HttpResponse.json(pro);
const nobodyOver = () => HttpResponse.json(previewPage([]));

/** The row of a key: a group named by the key's name. */
const row = (name: string) => within(screen.getByRole("group", { name }));

const limitField = (name: string) => row(name).getByRole("spinbutton");

const setLimit = async (name: string, value: string) => {
  const field = limitField(name);
  await userEvent.clear(field);
  if (value) await userEvent.type(field, value);
};

const save = () =>
  userEvent.click(screen.getByRole("button", { name: "Save changes" }));

/** A change of the name, so Save changes turns on (6.06): a plan saves only a change. */
const changeName = () =>
  userEvent.type(screen.getByRole("textbox", { name: "Name" }), " Plus");

const openPro = async () => {
  server.use(planAnswer(pro));
  renderWithProviders(<PlanEditor planId={pro.id} />);
  await screen.findByRole("heading", { level: 1, name: "Pro" });
};

describe("PlanEditor", () => {
  it("Edit_Pro_ShowsItsNameCountAndEveryKeyByArea", async () => {
    await openPro();

    expect(screen.getByText("6 organisations are on this plan.")).toBeVisible();
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("Pro");
    // The areas in the frames' order (6.04): the plan, then Members, Roles and Activity log.
    expect(
      screen
        .getAllByRole("heading", { level: 2 })
        .map((heading) => heading.textContent),
    ).toEqual(["Plan", "Members", "Roles", "Activity log"]);
    expect(
      row("Custom roles").getByRole("switch", { name: "Custom roles" }),
    ).toBeChecked();
    expect(row("Custom roles").queryByText("On")).toBeNull();
    expect(
      row("Custom roles").getByText(
        "Roles an organisation creates beyond Owner, Admin and Member.",
      ),
    ).toBeVisible();
    expect(row("Custom roles").queryByText(FeatureKey.RolesCustom)).toBeNull();
    expect(limitField("Custom roles limit")).toHaveValue(20);
    expect(
      row("Custom roles limit").getByRole("checkbox", { name: "Unlimited" }),
    ).not.toBeChecked();
    expect(
      row("Seats").getByRole("checkbox", { name: "Unlimited" }),
    ).toBeChecked();
    expect(limitField("Seats")).toBeDisabled();
    expect(screen.queryByText("Not set: closed")).toBeNull();
  });

  // 6.04: the Activity log's one row, its days with their unit, Unlimited, and the floor and the hold in its help.
  it("Edit_ActivityLog_ShowsKeepsEntriesWithTheFloorAndTheHold", async () => {
    await openPro();

    expect(limitField("Keeps entries")).toHaveValue(365);
    expect(row("Keeps entries").getByText("days")).toBeVisible();
    expect(
      row("Keeps entries").getByText(
        "Days, 30 or more. When the period gets shorter, older entries stay 30 days, then go.",
      ),
    ).toBeVisible();
    expect(
      row("Keeps entries").getByRole("checkbox", { name: "Unlimited" }),
    ).not.toBeChecked();
  });

  // 6.11: a period under the floor is refused before it is sent.
  it("Save_ActivityLogUnderTheFloor_SendsNothingAndShowsTheFloor", async () => {
    const calls: Call[] = [];
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [nobodyOver], calls),
      plansCommand("patch", `plans/${pro.id}`, [updated], calls),
    );

    await setLimit("Keeps entries", "7");
    await save();

    expect(
      await row("Keeps entries").findByText("Enter 30 days or more."),
    ).toBeVisible();
    expect(calls).toEqual([]);
  });

  // 6.19: a save that shortens the period of some organisations says how many, the periods and the hold.
  it("Save_ShortensTheActivityLog_ShowsTheOrganisationsAndTheHold", async () => {
    const updates: Call[] = [];
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [
        () =>
          HttpResponse.json(
            previewPage([], {
              retentionShortened: { count: 4, fromDays: 365, toDays: 90 },
            }),
          ),
      ]),
      plansCommand("patch", `plans/${pro.id}`, [updated], updates),
    );

    await setLimit("Keeps entries", "90");
    await save();

    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Save changes to Pro?" }),
    );
    expect(
      dialog.getByText(
        "This edit shortens the Activity log of 4 organisations from 365 to 90 days. Their entries older than 90 days stay 30 days, then go. To keep one as it is, give it an override first.",
      ),
    ).toBeVisible();
    expect(dialog.queryByRole("table")).toBeNull();
    expect(updates).toEqual([]);
    await userEvent.click(dialog.getByRole("button", { name: "Save anyway" }));
    await waitFor(() => expect(updates).toHaveLength(1));
  });

  it("Save_ShortensFromNoLimitForOneOrganisation_SaysNoTimeLimit", async () => {
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [
        () =>
          HttpResponse.json(
            previewPage([], {
              retentionShortened: { count: 1, fromDays: null, toDays: 90 },
            }),
          ),
      ]),
    );

    await setLimit("Keeps entries", "90");
    await save();

    expect(
      await screen.findByText(
        "This edit shortens the Activity log of 1 organisation from no time limit to 90 days. Its entries older than 90 days stay 30 days, then go. To keep it as it is, give it an override first.",
      ),
    ).toBeVisible();
  });

  // #271: a count of 0 says that no organisation follows the plan's shorter period (each has an override), so nothing is shortened.
  it("Save_ShortensButNoOrganisationFollows_SavesAtOnce", async () => {
    const updates: Call[] = [];
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [
        () =>
          HttpResponse.json(
            previewPage([], {
              retentionShortened: { count: 0, fromDays: 365, toDays: 90 },
            }),
          ),
      ]),
      plansCommand("patch", `plans/${pro.id}`, [updated], updates),
    );

    await setLimit("Keeps entries", "90");
    await save();

    await expectVisibleToast(sentence("Pro saved"));
    expect(updates).toHaveLength(1);
    expect(screen.queryByRole("alertdialog")).toBeNull();
  });

  it("Save_ShortensAndPutsOver_SaysBothAndListsTheOrganisations", async () => {
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [
        () =>
          HttpResponse.json(
            previewPage(
              [
                excess(
                  "0198f0a2-0000-7000-8000-000000000002",
                  "Burren Supply",
                  "burren-supply",
                  14,
                  10,
                ),
              ],
              { retentionShortened: { count: 4, fromDays: 365, toDays: 90 } },
            ),
          ),
      ]),
    );

    await setLimit("Custom roles limit", "10");
    await setLimit("Keeps entries", "90");
    await save();

    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Save changes to Pro?" }),
    );
    expect(
      dialog.getByText(
        /^This edit shortens the Activity log of 4 organisations/,
      ),
    ).toBeVisible();
    expect(
      dialog.getByText(/^This edit puts 1 organisation over a limit/),
    ).toBeVisible();
    expect(
      within(dialog.getByRole("table")).getByRole("link", {
        name: /Burren Supply/,
      }),
    ).toBeVisible();
  });

  it("Edit_OneOrganisation_SaysOne", async () => {
    server.use(planAnswer(free));

    renderWithProviders(<PlanEditor planId={free.id} />);

    expect(
      await screen.findByText("One organisation is on this plan."),
    ).toBeVisible();
  });

  it("Edit_KeyWithNoValue_SaysNotSetClosed", async () => {
    const { [LimitKey.MembersMax]: _, ...rest } = pro.entitlements;
    server.use(planAnswer({ ...pro, entitlements: rest }));

    renderWithProviders(<PlanEditor planId={pro.id} />);
    await screen.findByRole("heading", { level: 1, name: "Pro" });

    expect(row("Seats").queryByText("Not set: closed")).toBeNull();
    expect(limitField("Seats")).toHaveValue(null);
    expect(limitField("Seats")).toHaveAttribute("placeholder", "Not set");
    expect(row("Custom roles limit").queryByText("Not set: closed")).toBeNull();
  });

  it("Edit_FeatureWithNoValue_IsOffAndNotSet", async () => {
    const { [FeatureKey.RolesCustom]: _, ...rest } = pro.entitlements;
    server.use(planAnswer({ ...pro, entitlements: rest }));

    renderWithProviders(<PlanEditor planId={pro.id} />);
    await screen.findByRole("heading", { level: 1, name: "Pro" });

    expect(row("Custom roles").getByRole("switch")).not.toBeChecked();
    expect(row("Custom roles").getByText("Not set: closed")).toBeVisible();
    expect(row("Custom roles").queryByText("Off")).toBeNull();
  });

  it("Edit_NotTheDefault_OffersTheMarkInPlaceOfTheDefaultPlan", async () => {
    await openPro();

    const mark = screen.getByRole("checkbox", { name: "Default plan" });
    expect(mark).not.toBeChecked();
    expect(mark).toBeEnabled();
    await waitFor(() =>
      expect(mark).toHaveAccessibleDescription(
        "Check it to give this plan to new organisations instead of Free.",
      ),
    );
    await userEvent.click(mark);
    expect(mark).toHaveAccessibleDescription(
      "New organisations get this plan.",
    );
  });

  it("Edit_TheDefaultPlan_KeepsTheMarkCheckedAndDisabledWithItsReason", async () => {
    server.use(planAnswer(free));

    renderWithProviders(<PlanEditor planId={free.id} />);

    const mark = await screen.findByRole("checkbox", { name: "Default plan" });
    expect(mark).toBeChecked();
    expect(mark).toBeDisabled();
    expect(mark).toHaveAccessibleDescription(
      "There is always one default plan. To change it, make another plan the default first.",
    );
  });

  it("Save_RaisesALimit_SendsEveryKeyWithoutAPreview", async () => {
    const updates: Call[] = [];
    const previews: Call[] = [];
    await openPro();
    server.use(
      plansCommand("patch", `plans/${pro.id}`, [updated], updates),
      plansCommand("post", `plans/${pro.id}/preview`, [nobodyOver], previews),
    );

    await setLimit("Custom roles limit", "30");
    await save();

    await expectVisibleToast(sentence("Pro saved"));
    expect(previews).toEqual([]);
    expect(updates.map((call) => call.body)).toEqual([
      {
        name: "Pro",
        isDefault: false,
        entitlements: {
          [FeatureKey.RolesCustom]: { enabled: true },
          [LimitKey.RolesCustomMax]: { maximum: 30 },
          [LimitKey.MembersMax]: { unlimited: true },
          [LimitKey.AuditRetentionDays]: { maximum: 365 },
        },
      },
    ]);
    // 6.08: the person stays on the plan, in its saved state.
    expect(router.push).not.toHaveBeenCalled();
    expect(
      screen.getByText("The 6 organisations on it get the new values at once."),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
  });

  it("Save_NoChange_IsDisabled", async () => {
    await openPro();

    expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
  });

  it("Save_ChangeThenChangeBack_IsEnabledThenDisabledAgain", async () => {
    await openPro();

    await setLimit("Custom roles limit", "30");
    expect(screen.getByRole("button", { name: "Save changes" })).toBeEnabled();
    await setLimit("Custom roles limit", "20");

    expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
  });

  it("Save_PlanWithNoOrganisation_ToastSaysNoCount", async () => {
    server.use(
      planAnswer(partner),
      plansCommand("patch", `plans/${partner.id}`, [
        () => HttpResponse.json(partner),
      ]),
    );
    renderWithProviders(<PlanEditor planId={partner.id} />);
    await screen.findByRole("heading", { level: 1, name: "Partner" });

    await setLimit("Custom roles limit", "60");
    await save();

    await expectVisibleToast(sentence("Partner saved"));
    expect(screen.queryByText(/get the new values at once/)).toBeNull();
  });

  it("Save_KeyLeftUnset_SendsEveryOtherKeyAndLeavesItClosed", async () => {
    const updates: Call[] = [];
    const { [LimitKey.MembersMax]: _, ...rest } = pro.entitlements;
    server.use(
      planAnswer({ ...pro, entitlements: rest }),
      plansCommand("patch", `plans/${pro.id}`, [updated], updates),
    );

    renderWithProviders(<PlanEditor planId={pro.id} />);
    await screen.findByRole("heading", { level: 1, name: "Pro" });
    await userEvent.clear(screen.getByRole("textbox", { name: "Name" }));
    await userEvent.type(
      screen.getByRole("textbox", { name: "Name" }),
      "Pro 2",
    );
    await save();

    await waitFor(() => expect(updates).toHaveLength(1));
    expect(updates[0]?.body).toEqual({
      name: "Pro 2",
      isDefault: false,
      entitlements: {
        [FeatureKey.RolesCustom]: { enabled: true },
        [LimitKey.RolesCustomMax]: { maximum: 20 },
        [LimitKey.AuditRetentionDays]: { maximum: 365 },
      },
    });
  });

  it("Save_ClearsALimit_PreviewsBecauseTheKeyCloses", async () => {
    const previews: Call[] = [];
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [nobodyOver], previews),
      plansCommand("patch", `plans/${pro.id}`, [updated]),
    );

    await setLimit("Custom roles limit", "");
    expect(limitField("Custom roles limit")).toHaveAttribute(
      "placeholder",
      "Not set",
    );
    await save();

    await waitFor(() => expect(previews).toHaveLength(1));
    expect(previews[0]?.body).toEqual({
      entitlements: {
        [FeatureKey.RolesCustom]: { enabled: true },
        [LimitKey.MembersMax]: { unlimited: true },
        [LimitKey.AuditRetentionDays]: { maximum: 365 },
      },
    });
  });

  it("Save_LowersALimitAndNobodyGoesOver_SavesAtOnce", async () => {
    const updates: Call[] = [];
    const previews: Call[] = [];
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [nobodyOver], previews),
      plansCommand("patch", `plans/${pro.id}`, [updated], updates),
    );

    await setLimit("Custom roles limit", "10");
    await save();

    await expectVisibleToast(sentence("Pro saved"));
    expect(previews).toHaveLength(1);
    expect(updates).toHaveLength(1);
    expect(screen.queryByRole("alertdialog")).toBeNull();
  });

  it("Save_TurnsAFeatureOff_Previews", async () => {
    const previews: Call[] = [];
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [nobodyOver], previews),
      plansCommand("patch", `plans/${pro.id}`, [updated]),
    );

    await userEvent.click(row("Custom roles").getByRole("switch"));
    expect(row("Custom roles").getByRole("switch")).not.toBeChecked();
    await save();

    await waitFor(() => expect(previews).toHaveLength(1));
    expect(previews[0]?.body).toEqual({
      entitlements: {
        [FeatureKey.RolesCustom]: { enabled: false },
        [LimitKey.RolesCustomMax]: { maximum: 20 },
        [LimitKey.MembersMax]: { unlimited: true },
        [LimitKey.AuditRetentionDays]: { maximum: 365 },
      },
    });
  });

  it("Save_UnlimitedToANumber_Previews", async () => {
    const previews: Call[] = [];
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [nobodyOver], previews),
      plansCommand("patch", `plans/${pro.id}`, [updated]),
    );

    await userEvent.click(
      row("Seats").getByRole("checkbox", { name: "Unlimited" }),
    );
    await setLimit("Seats", "100");
    await save();

    await waitFor(() => expect(previews).toHaveLength(1));
    expect(previews[0]?.body).toMatchObject({
      entitlements: { [LimitKey.MembersMax]: { maximum: 100 } },
    });
  });

  it("Save_SomeGoOver_ShowsTheImpactWithEachOrganisation", async () => {
    const updates: Call[] = [];
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [
        () =>
          HttpResponse.json(
            previewPage([
              excess(
                "0198f0a2-0000-7000-8000-000000000002",
                "Burren Supply",
                "burren-supply",
                14,
                10,
              ),
              excess(
                "0198f0a2-0000-7000-8000-000000000003",
                "Shannon Studio",
                "shannon-studio",
                12,
                10,
              ),
            ]),
          ),
      ]),
      plansCommand("patch", `plans/${pro.id}`, [updated], updates),
    );

    await setLimit("Custom roles limit", "10");
    await save();

    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Save changes to Pro?" }),
    );
    expect(
      dialog.getByText(
        "This edit puts 2 organisations over a limit. Nothing is deleted or locked: they can’t add more until they are under it. To keep one as it is, give it an override first.",
      ),
    ).toBeVisible();
    const burren = within(dialog.getByRole("table")).getByRole("link", {
      name: /Burren Supply/,
    });
    expect(burren).toHaveAttribute(
      "href",
      "/organisations/0198f0a2-0000-7000-8000-000000000002",
    );
    expect(burren).toHaveAttribute("target", "_blank");
    const burrenRow = burren.closest("tr") as HTMLElement;
    expect(within(burrenRow).getByText("Custom roles limit")).toBeVisible();
    expect(within(burrenRow).getByText("14")).toBeVisible();
    expect(within(burrenRow).getByText("Over by 4")).toBeVisible();
    expect(within(burrenRow).getByText("10")).toBeVisible();
    expect(updates).toEqual([]);
  });

  it("Save_SaveAnyway_SendsTheEdit", async () => {
    const updates: Call[] = [];
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [
        () =>
          HttpResponse.json(
            previewPage([
              excess(
                "0198f0a2-0000-7000-8000-000000000002",
                "Burren Supply",
                "burren-supply",
                14,
                10,
              ),
            ]),
          ),
      ]),
      plansCommand("patch", `plans/${pro.id}`, [updated], updates),
    );

    await setLimit("Custom roles limit", "10");
    await save();
    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Save changes to Pro?" }),
    );
    await userEvent.click(dialog.getByRole("button", { name: "Save anyway" }));

    await expectVisibleToast(sentence("Pro saved"));
    expect(updates.map((call) => call.body)).toEqual([
      {
        name: "Pro",
        isDefault: false,
        entitlements: {
          [FeatureKey.RolesCustom]: { enabled: true },
          [LimitKey.RolesCustomMax]: { maximum: 10 },
          [LimitKey.MembersMax]: { unlimited: true },
          [LimitKey.AuditRetentionDays]: { maximum: 365 },
        },
      },
    ]);
  });

  it("Save_CancelTheImpact_KeepsTheEditOnScreen", async () => {
    const updates: Call[] = [];
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [
        () =>
          HttpResponse.json(
            previewPage([
              excess(
                "0198f0a2-0000-7000-8000-000000000002",
                "Burren Supply",
                "burren-supply",
                14,
                10,
              ),
            ]),
          ),
      ]),
      plansCommand("patch", `plans/${pro.id}`, [updated], updates),
    );

    await setLimit("Custom roles limit", "10");
    await save();
    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Save changes to Pro?" }),
    );
    await userEvent.click(dialog.getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByRole("alertdialog")).toBeNull());
    expect(limitField("Custom roles limit")).toHaveValue(10);
    expect(updates).toEqual([]);
  });

  it("Impact_LoadMore_AsksTheNextPage", async () => {
    const previews: Call[] = [];
    await openPro();
    server.use(
      plansCommand(
        "post",
        `plans/${pro.id}/preview`,
        [
          () =>
            HttpResponse.json(
              previewPage(
                [
                  excess(
                    "0198f0a2-0000-7000-8000-000000000002",
                    "Burren Supply",
                    "burren-supply",
                    14,
                    10,
                  ),
                ],
                { total: 2, pageSize: 1 },
              ),
            ),
          () =>
            HttpResponse.json(
              previewPage(
                [
                  excess(
                    "0198f0a2-0000-7000-8000-000000000003",
                    "Shannon Studio",
                    "shannon-studio",
                    12,
                    10,
                  ),
                ],
                { total: 2, pageSize: 1, page: 2 },
              ),
            ),
        ],
        previews,
      ),
    );

    await setLimit("Custom roles limit", "10");
    await save();
    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Save changes to Pro?" }),
    );
    await userEvent.click(dialog.getByRole("button", { name: "Load more" }));

    expect(
      await within(dialog.getByRole("table")).findByRole("link", {
        name: /Shannon Studio/,
      }),
    ).toBeVisible();
    expect(
      within(dialog.getByRole("table")).getByRole("link", {
        name: /Burren Supply/,
      }),
    ).toBeVisible();
    expect(previews.at(-1)?.url.searchParams.get("page")).toBe("2");
  });

  it("Impact_FeatureTurnedOff_ListsWhatUsesIt", async () => {
    await openPro();
    server.use(
      plansCommand("post", `plans/${pro.id}/preview`, [
        () =>
          HttpResponse.json(
            previewPage([
              {
                tenantId: "0198f0a2-0000-7000-8000-000000000002",
                name: "Burren Supply",
                slug: "burren-supply",
                furthest: 14,
                excesses: [
                  {
                    key: FeatureKey.RolesCustom,
                    count: 14,
                    newValue: {
                      enabled: false,
                      maximum: null,
                      unlimited: false,
                    },
                    amount: 14,
                  },
                ],
                overriddenKeys: [],
              },
            ]),
          ),
      ]),
    );

    await userEvent.click(row("Custom roles").getByRole("switch"));
    await save();

    const dialog = within(
      await screen.findByRole("alertdialog", { name: "Save changes to Pro?" }),
    );
    const burrenRow = within(dialog.getByRole("table"))
      .getByRole("link", { name: /Burren Supply/ })
      .closest("tr") as HTMLElement;
    expect(within(burrenRow).getByText("Custom roles")).toBeVisible();
    expect(within(burrenRow).getByText("14")).toBeVisible();
    expect(within(burrenRow).getByText("Off")).toBeVisible();
  });

  it("Save_NameTaken_ShowsItUnderTheName", async () => {
    await openPro();
    server.use(
      plansCommand("patch", `plans/${pro.id}`, [
        () => problem(409, ErrorCode.PlanNameTaken),
      ]),
    );

    await userEvent.clear(screen.getByRole("textbox", { name: "Name" }));
    await userEvent.type(screen.getByRole("textbox", { name: "Name" }), "Free");
    await save();

    expect(
      await screen.findByText("Another plan has this name."),
    ).toBeVisible();
    expect(
      screen.getByRole("textbox", { name: "Name" }),
    ).toHaveAccessibleDescription("Another plan has this name.");
  });

  it("Save_NameInvalid_ShowsEnterAName", async () => {
    await openPro();
    server.use(
      plansCommand("patch", `plans/${pro.id}`, [
        () => problem(400, ErrorCode.PlanNameInvalid),
      ]),
    );

    await changeName();
    await save();

    expect(await screen.findByText("Enter a name.")).toBeVisible();
  });

  it("Save_EmptyName_SendsNothingAndShowsEnterAName", async () => {
    const updates: Call[] = [];
    await openPro();
    server.use(plansCommand("patch", `plans/${pro.id}`, [updated], updates));

    await userEvent.clear(screen.getByRole("textbox", { name: "Name" }));
    await save();

    expect(await screen.findByText("Enter a name.")).toBeVisible();
    expect(updates).toEqual([]);
  });

  it("Save_NameTooLong_ShowsTheLimit", async () => {
    await openPro();
    server.use(
      plansCommand("patch", `plans/${pro.id}`, [
        () =>
          problem(400, ErrorCode.RequestValidationFailed, {
            name: [FieldErrorCode.TooLong],
          }),
      ]),
    );

    await changeName();
    await save();

    expect(await screen.findByText("Use 1 to 100 characters.")).toBeVisible();
  });

  it("Save_NegativeLimit_SendsNothingAndShowsTheRule", async () => {
    const updates: Call[] = [];
    await openPro();
    server.use(plansCommand("patch", `plans/${pro.id}`, [updated], updates));

    await setLimit("Custom roles limit", "-1");
    await save();

    expect(
      await row("Custom roles limit").findByText(
        "Enter a whole number, 0 or more.",
      ),
    ).toBeVisible();
    expect(updates).toEqual([]);
  });

  it("Save_EntitlementInvalid_ShowsTheRuleUnderItsKey", async () => {
    await openPro();
    server.use(
      plansCommand("patch", `plans/${pro.id}`, [
        () =>
          HttpResponse.json(
            {
              status: 400,
              title: "Refused",
              code: ErrorCode.PlanEntitlementInvalid,
              entitlement: LimitKey.MembersMax,
              traceId:
                "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            },
            { status: 400 },
          ),
      ]),
    );

    await changeName();
    await save();

    expect(
      await row("Seats").findByText("Enter a whole number, 0 or more."),
    ).toBeVisible();
  });

  it("Save_DefaultRequired_ShowsItsTextAndReadsThePlanAgain", async () => {
    const reads = { count: 0 };
    server.use(
      planAnswer(free, reads),
      plansCommand("patch", `plans/${free.id}`, [
        () => problem(409, ErrorCode.PlanDefaultRequired),
      ]),
    );

    renderWithProviders(<PlanEditor planId={free.id} />);
    await screen.findByRole("heading", { level: 1, name: "Free" });
    await changeName();
    await save();

    const banner = (await screen.findByText("Plan not saved")).closest(
      "[role=status]",
    );
    expect(banner).toHaveTextContent(
      "There is always one default plan. To change it, make another plan the default first.",
    );
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Save_PlanNotFound_ShowsItsTextAndReadsAgain", async () => {
    const reads = { count: 0 };
    server.use(
      planAnswer(pro, reads),
      plansCommand("patch", `plans/${pro.id}`, [
        () => problem(404, ErrorCode.PlanNotFound),
      ]),
    );

    renderWithProviders(<PlanEditor planId={pro.id} />);
    await screen.findByRole("heading", { level: 1, name: "Pro" });
    await changeName();
    await save();

    expect(
      await screen.findByText("This plan no longer exists."),
    ).toBeVisible();
    await waitFor(() => expect(reads.count).toBe(2));
  });

  it("Edit_UnknownPlan_ShowsNotFound", async () => {
    renderWithProviders(<PlanEditor planId="not-a-uuid" />);

    expect(await screen.findByText("Page not found")).toBeVisible();
  });

  it("Create_Empty_StartsUnsetAndSendsTheValuesSet", async () => {
    const creates: Call[] = [];
    server.use(
      plansCommand(
        "post",
        "plans",
        [
          () =>
            HttpResponse.json(
              { ...partner, name: "Starter" },
              {
                status: 201,
                headers: { Location: `/api/v1/platform/plans/${partner.id}` },
              },
            ),
        ],
        creates,
      ),
    );

    renderWithProviders(<PlanEditor planId={null} />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "New plan" }),
    ).toBeVisible();
    expect(
      screen.getByText(
        "A key you leave unset is closed for the organisations on this plan.",
      ),
    ).toBeVisible();
    // Only the feature says it; an unset limit shows its placeholder (6.10).
    expect(screen.getAllByText("Not set: closed")).toHaveLength(1);
    await userEvent.type(
      screen.getByRole("textbox", { name: "Name" }),
      "Starter",
    );
    await userEvent.click(row("Custom roles").getByRole("switch"));
    await setLimit("Seats", "10");
    await userEvent.click(screen.getByRole("button", { name: "Create plan" }));

    await expectVisibleToast(sentence("Starter created"));
    expect(creates.map((call) => call.body)).toEqual([
      {
        name: "Starter",
        isDefault: false,
        entitlements: {
          [FeatureKey.RolesCustom]: { enabled: true },
          [LimitKey.MembersMax]: { maximum: 10 },
        },
      },
    ]);
    expect(creates[0]?.key).toMatch(/^[0-9a-f-]{36}$/);
    // 6.12: the new plan opens on its own page.
    expect(router.push).toHaveBeenCalledWith(`/plans/${partner.id}`);
    expect(getToken).toHaveBeenCalledBefore(router.push);
    expect(
      screen.getByText(
        "Give it to an organisation from that organisation’s Plan tab.",
      ),
    ).toBeVisible();
  });

  it("Create_Refused_RetriesWithANewKey", async () => {
    const creates: Call[] = [];
    server.use(
      plansCommand(
        "post",
        "plans",
        [
          () => problem(409, ErrorCode.PlanNameTaken),
          () => HttpResponse.json(partner, { status: 201 }),
        ],
        creates,
      ),
    );

    renderWithProviders(<PlanEditor planId={null} />);
    await userEvent.type(
      await screen.findByRole("textbox", { name: "Name" }),
      "Free",
    );
    await userEvent.click(screen.getByRole("button", { name: "Create plan" }));
    expect(
      await screen.findByText("Another plan has this name."),
    ).toBeVisible();
    await userEvent.click(screen.getByRole("button", { name: "Create plan" }));

    await waitFor(() => expect(creates).toHaveLength(2));
    expect(creates[1]?.key).not.toBe(creates[0]?.key);
  });

  it("Create_RetryAfterANetworkError_SendsTheSameKey", async () => {
    const creates: Call[] = [];
    server.use(
      plansCommand(
        "post",
        "plans",
        [
          () => HttpResponse.error(),
          () => HttpResponse.json(partner, { status: 201 }),
        ],
        creates,
      ),
    );

    renderWithProviders(<PlanEditor planId={null} />);
    await userEvent.type(
      await screen.findByRole("textbox", { name: "Name" }),
      "Partner",
    );
    await userEvent.click(screen.getByRole("button", { name: "Create plan" }));
    await waitFor(() => expect(creates).toHaveLength(1));
    await userEvent.click(screen.getByRole("button", { name: "Create plan" }));

    await waitFor(() => expect(creates).toHaveLength(2));
    expect(creates[1]?.key).toBe(creates[0]?.key);
  });

  it("Create_EditAfterANetworkError_SendsANewKey", async () => {
    const creates: Call[] = [];
    server.use(
      plansCommand(
        "post",
        "plans",
        [
          () => HttpResponse.error(),
          () => HttpResponse.json(partner, { status: 201 }),
        ],
        creates,
      ),
    );

    renderWithProviders(<PlanEditor planId={null} />);
    const name = await screen.findByRole("textbox", { name: "Name" });
    await userEvent.type(name, "Partner");
    await userEvent.click(screen.getByRole("button", { name: "Create plan" }));
    await waitFor(() => expect(creates).toHaveLength(1));
    await userEvent.type(name, "s");
    await userEvent.click(screen.getByRole("button", { name: "Create plan" }));

    await waitFor(() => expect(creates).toHaveLength(2));
    expect(creates[1]?.key).not.toBe(creates[0]?.key);
  });

  it("Back_LeadsBackToThePlans", async () => {
    await openPro();

    expect(screen.queryByRole("link", { name: "Cancel" })).toBeNull();
    expect(screen.getByRole("link", { name: "Plans" })).toHaveAttribute(
      "href",
      "/plans",
    );
  });

  it("Unlimited_Unchecked_EnablesTheNumber", async () => {
    await openPro();

    await userEvent.click(
      row("Seats").getByRole("checkbox", { name: "Unlimited" }),
    );

    expect(limitField("Seats")).toBeEnabled();
    expect(limitField("Seats")).toHaveAttribute("placeholder", "Not set");
    await setLimit("Seats", "40");
    expect(limitField("Seats")).toHaveValue(40);
  });

  it("Edit_DefaultHint_NamesTheDefaultPlanFromTheList", async () => {
    server.use(
      plansAnswer(plansPage([{ ...free, name: "Basic" }, pro])),
      planAnswer(pro),
    );

    renderWithProviders(<PlanEditor planId={pro.id} />);

    const mark = await screen.findByRole("checkbox", { name: "Default plan" });
    await waitFor(() =>
      expect(mark).toHaveAccessibleDescription(
        "Check it to give this plan to new organisations instead of Basic.",
      ),
    );
  });

  // 6.18: on a phone the plan opens as a summary of its values, with Edit plan (CSS hides it from bp-tablet).
  it("Summary_Pro_ListsEachValueAndOffersEditPlan", async () => {
    await openPro();

    const summary = within(screen.getByRole("region", { name: "Limits" }));
    /** The value under a key's name in the summary. */
    const value = (name: string) =>
      summary.getByText(name, { selector: "dt" }).nextElementSibling
        ?.textContent;
    expect([
      value("Seats"),
      value("Custom roles"),
      value("Custom roles limit"),
      value("Activity log"),
    ]).toEqual(["Unlimited", "On", "20", "365 days"]);
    expect(
      screen.getByRole("button", { name: "Edit plan" }),
    ).toBeInTheDocument();
  });
});
