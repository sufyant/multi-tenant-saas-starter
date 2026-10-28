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
  usePathname: () => "/settings/plans",
}));

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
    expect(
      screen.getByRole("heading", { level: 2, name: "Roles" }),
    ).toBeVisible();
    expect(
      screen.getByRole("heading", { level: 2, name: "Members" }),
    ).toBeVisible();
    expect(
      row("Custom roles").getByRole("switch", { name: "Custom roles" }),
    ).toBeChecked();
    expect(row("Custom roles").getByText("On")).toBeVisible();
    expect(
      row("Custom roles").getByText(
        "Roles an organisation creates beyond Owner, Admin and Member.",
      ),
    ).toBeVisible();
    expect(row("Custom roles").getByText(FeatureKey.RolesCustom)).toBeVisible();
    expect(limitField("Custom roles limit")).toHaveValue(20);
    expect(
      row("Custom roles limit").getByRole("checkbox", { name: "Unlimited" }),
    ).not.toBeChecked();
    expect(
      row("Members limit").getByRole("checkbox", { name: "Unlimited" }),
    ).toBeChecked();
    expect(limitField("Members limit")).toBeDisabled();
    expect(screen.queryByText("Not set: closed")).toBeNull();
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

    expect(row("Members limit").getByText("Not set: closed")).toBeVisible();
    expect(limitField("Members limit")).toHaveValue(null);
    expect(limitField("Members limit")).toHaveAttribute(
      "placeholder",
      "Not set",
    );
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
        },
      },
    ]);
    expect(router.push).toHaveBeenCalledWith("/settings/plans");
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
    expect(
      row("Custom roles limit").getByText("Not set: closed"),
    ).toBeVisible();
    await save();

    await waitFor(() => expect(previews).toHaveLength(1));
    expect(previews[0]?.body).toEqual({
      entitlements: {
        [FeatureKey.RolesCustom]: { enabled: true },
        [LimitKey.MembersMax]: { unlimited: true },
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
    expect(row("Custom roles").getByText("Off")).toBeVisible();
    await save();

    await waitFor(() => expect(previews).toHaveLength(1));
    expect(previews[0]?.body).toEqual({
      entitlements: {
        [FeatureKey.RolesCustom]: { enabled: false },
        [LimitKey.RolesCustomMax]: { maximum: 20 },
        [LimitKey.MembersMax]: { unlimited: true },
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
      row("Members limit").getByRole("checkbox", { name: "Unlimited" }),
    );
    await setLimit("Members limit", "100");
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

  it("Impact_Search_AsksThePreviewWithTheWords", async () => {
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
                { total: 200 },
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
    expect(
      dialog.getByText("Showing 1 of 200, the furthest over first."),
    ).toBeVisible();
    await userEvent.type(
      dialog.getByRole("searchbox", { name: "Search organisations" }),
      "burren",
    );

    await waitFor(() =>
      expect(previews.at(-1)?.url.searchParams.get("q")).toBe("burren"),
    );
    expect(previews.at(-1)?.body).toEqual(previews[0]?.body);
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

    await save();

    expect(
      await row("Members limit").findByText("Enter a whole number, 0 or more."),
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
    expect(screen.getAllByText("Not set: closed")).toHaveLength(3);
    await userEvent.type(
      screen.getByRole("textbox", { name: "Name" }),
      "Starter",
    );
    await userEvent.click(row("Custom roles").getByRole("switch"));
    await setLimit("Members limit", "10");
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
    expect(router.push).toHaveBeenCalledWith("/settings/plans");
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

  it("Cancel_LeadsBackToThePlans", async () => {
    await openPro();

    expect(screen.getByRole("link", { name: "Cancel" })).toHaveAttribute(
      "href",
      "/settings/plans",
    );
  });

  it("Unlimited_Unchecked_EnablesTheNumber", async () => {
    await openPro();

    await userEvent.click(
      row("Members limit").getByRole("checkbox", { name: "Unlimited" }),
    );

    expect(limitField("Members limit")).toBeEnabled();
    expect(row("Members limit").getByText("Not set: closed")).toBeVisible();
    await setLimit("Members limit", "40");
    expect(row("Members limit").queryByText("Not set: closed")).toBeNull();
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
});
