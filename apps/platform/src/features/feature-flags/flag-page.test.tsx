import { ErrorCode } from "@repo/api-client";
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
  bulkEditDetail,
  csvImportDetail,
  emailsOffDetail,
  emailsSend,
  flagAnswer,
  northwindId,
  previewAnswer,
} from "../../../test/feature-flags";
import {
  atlas,
  listAnswer,
  meAnswer,
  northwind,
  pageOf,
  problem,
} from "../../../test/organisations";
import { renderWithProviders, sentence } from "../../../test/render";
import { FlagPage } from "./flag-page";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const rules = async () =>
  within(await screen.findByRole("region", { name: "Who it is on for" }));

describe("FlagPage", () => {
  it("FlagPage_Release_ShowsItsValuesRulesAndHistory", async () => {
    server.use(meAnswer(), flagAnswer(bulkEditDetail));

    renderWithProviders(<FlagPage flagKey="roles.bulk_edit" />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "roles.bulk_edit" }),
    ).toBeVisible();
    expect(screen.getByText("Release flag · defined in code")).toBeVisible();
    expect(
      screen.getByText("Off. It applies when the check fails."),
    ).toBeVisible();
    expect(screen.getByText("14 Oct 2026 · 3f9e21c")).toBeVisible();
    const rule = await rules();
    expect(
      rule.getByRole("link", { name: "Northwind Traders" }),
    ).toHaveAttribute("href", `/organisations/${northwindId}`);
    expect(rule.getByText("10%")).toBeVisible();
    expect(
      rule.getByRole("button", { name: "Turn on for everyone" }),
    ).toBeVisible();
    const history = within(screen.getByRole("region", { name: "History" }));
    expect(history.getByText("Percentage 0% → 10%")).toBeVisible();
    expect(history.getByText("28 Oct 2026, 14:02 · Deniz Kaya")).toBeVisible();
    expect(
      history.getByText(sentence("Listed Northwind Traders")),
    ).toBeVisible();
    expect(
      history.getByText("Added to code · 14 Oct 2026 · 3f9e21c"),
    ).toBeVisible();
    expect(history.getByText("Default value off.")).toBeVisible();
  });

  it("FlagPage_KillSwitchOff_SaysSinceWhenAndWhoTurnedItOff", async () => {
    server.use(meAnswer(), flagAnswer(emailsOffDetail));

    renderWithProviders(<FlagPage flagKey="emails.send" />);

    expect(
      await screen.findByText("Off for everyone since 10:12"),
    ).toBeVisible();
    expect(
      screen.getByText("Turn it back on when the problem is fixed."),
    ).toBeVisible();
    expect(
      screen.getByText("Turned off by Deniz Kaya at 10:12."),
    ).toBeVisible();
    expect(
      (await rules()).getByRole("button", { name: "Turn on for everyone" }),
    ).toBeVisible();
  });

  it("FlagPage_RemovedFromCode_IsReadOnlyWithItsBanner", async () => {
    server.use(meAnswer(), flagAnswer(csvImportDetail));

    renderWithProviders(<FlagPage flagKey="invitations.csv_import" />);

    expect(
      await screen.findByText(
        "Removed from code · 20 Oct 2026 · 7c41d09. This record is archived and read-only. Its name is never used again.",
      ),
    ).toBeVisible();
    expect((await rules()).queryByRole("button")).toBeNull();
  });

  it("FlagPage_UnknownKey_SaysPageNotFoundWithTheWayBack", async () => {
    server.use(
      meAnswer(),
      http.get("*/v1/platform/feature-flags/:key", () =>
        problem(404, ErrorCode.FeatureFlagNotFound),
      ),
    );

    renderWithProviders(<FlagPage flagKey="nothing.here" />);

    expect(await screen.findByText("Page not found")).toBeVisible();
    expect(
      screen.getByText(
        "No flag has this name. Check the link, or go back to Feature flags.",
      ),
    ).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Feature flags" }),
    ).toHaveAttribute("href", "/settings/feature-flags");
  });

  it("FlagPage_ReadFails_SaysItCouldNotLoadThisFlag", async () => {
    server.use(
      meAnswer(),
      http.get("*/v1/platform/feature-flags/:key", () =>
        problem(500, ErrorCode.ServerError),
      ),
    );

    renderWithProviders(<FlagPage flagKey="roles.bulk_edit" />);

    expect(await screen.findByText("Couldn’t load this flag")).toBeVisible();
  });

  it("FlagPage_ChangePercentage_ShowsTheCountThenSendsTheNewPercentage", async () => {
    const bodies: unknown[] = [];
    const sent: unknown[] = [];
    server.use(
      meAnswer(),
      flagAnswer(bulkEditDetail),
      previewAnswer(2, bodies),
      http.put(
        "*/v1/platform/feature-flags/:key/percentage",
        async ({ request }) => {
          sent.push(await request.json());
          return HttpResponse.json({
            ...bulkEditDetail,
            flag: { ...bulkEditDetail.flag, percentage: 50 },
          });
        },
      ),
    );
    renderWithProviders(<FlagPage flagKey="roles.bulk_edit" />);
    await userEvent.click(
      (await rules()).getByRole("button", { name: "Change" }),
    );
    const dialog = within(await screen.findByRole("alertdialog"));
    expect(
      dialog.getByText("Change the percentage in Production"),
    ).toBeVisible();

    await userEvent.click(dialog.getByRole("combobox", { name: "Percentage" }));
    await userEvent.click(await screen.findByRole("option", { name: "50%" }));

    expect(
      await dialog.findByText(
        "This change affects about 2 organisations. Organisations that are in at 10% stay in.",
      ),
    ).toBeVisible();
    expect(bodies).toEqual([
      {
        tenantToAdd: null,
        tenantToRemove: null,
        percentage: 50,
        everyoneElse: null,
      },
    ]);
    await userEvent.click(
      dialog.getByRole("button", { name: "Change to 50%" }),
    );
    expect(sent).toEqual([{ percentage: 50 }]);
    expect(
      await screen.findByText("It is on for 50% of the other organisations."),
    ).toBeVisible();
  });

  it("FlagPage_TurnOnForEveryone_NamesTheEnvironmentAndTheCount", async () => {
    const sent: unknown[] = [];
    server.use(
      meAnswer(),
      flagAnswer(bulkEditDetail),
      previewAnswer(8),
      http.put(
        "*/v1/platform/feature-flags/:key/everyone-else",
        async ({ request }) => {
          sent.push(await request.json());
          return HttpResponse.json(bulkEditDetail);
        },
      ),
    );
    renderWithProviders(<FlagPage flagKey="roles.bulk_edit" />);

    await userEvent.click(
      (await rules()).getByRole("button", { name: "Turn on for everyone" }),
    );

    const dialog = within(await screen.findByRole("alertdialog"));
    expect(
      dialog.getByText(
        sentence("Turn roles.bulk_edit on for everyone in Production?"),
      ),
    ).toBeVisible();
    expect(
      await dialog.findByText(
        "8 more organisations get it now: every organisation that is not listed and not in the 10%. You can turn it off for everyone at any time.",
      ),
    ).toBeVisible();
    await userEvent.click(
      dialog.getByRole("button", { name: "Turn on for everyone" }),
    );
    expect(sent).toEqual([{ on: true }]);
  });

  it("FlagPage_RemoveListed_AsksThenSendsTheRemoval", async () => {
    const removed: string[] = [];
    server.use(
      meAnswer(),
      flagAnswer(bulkEditDetail),
      http.delete(
        "*/v1/platform/feature-flags/:key/tenants/:tenantId",
        ({ params }) => {
          removed.push(String(params.tenantId));
          return HttpResponse.json({ ...bulkEditDetail, listed: [] });
        },
      ),
    );
    renderWithProviders(<FlagPage flagKey="roles.bulk_edit" />);

    await userEvent.click(
      (await rules()).getByRole("button", { name: "Remove Northwind Traders" }),
    );
    const dialog = within(await screen.findByRole("alertdialog"));
    expect(
      dialog.getByText(
        sentence("Remove Northwind Traders from the list in Production?"),
      ),
    ).toBeVisible();
    await userEvent.click(dialog.getByRole("button", { name: "Remove" }));

    expect(removed).toEqual([northwindId]);
  });
  it("FlagPage_AddOrganisation_OffersOnlyTheUnlistedAndSendsTheChoice", async () => {
    const added: string[] = [];
    server.use(
      meAnswer(),
      flagAnswer(bulkEditDetail),
      listAnswer(pageOf([northwind, atlas])),
      previewAnswer(1),
      http.put(
        "*/v1/platform/feature-flags/:key/tenants/:tenantId",
        ({ params }) => {
          added.push(String(params.tenantId));
          return HttpResponse.json(bulkEditDetail);
        },
      ),
    );
    renderWithProviders(<FlagPage flagKey="roles.bulk_edit" />);
    await userEvent.click(
      (await rules()).getByRole("button", { name: "Add organisation" }),
    );
    const dialog = within(await screen.findByRole("alertdialog"));

    await userEvent.click(
      dialog.getByRole("button", { name: "Add organisation" }),
    );
    expect(await dialog.findByText("Choose an organisation.")).toBeVisible();
    await userEvent.click(
      dialog.getByRole("combobox", { name: "Organisation" }),
    );
    expect(
      (await screen.findAllByRole("option")).map(
        (option) => option.textContent,
      ),
    ).toEqual(["Atlas Logistics"]);
    await userEvent.click(
      screen.getByRole("option", { name: "Atlas Logistics" }),
    );
    expect(
      await dialog.findByText("This change affects 1 organisation."),
    ).toBeVisible();
    await userEvent.click(
      dialog.getByRole("button", { name: "Add organisation" }),
    );

    expect(added).toEqual([atlas.id]);
  });

  it("FlagPage_ChangeRefusedAsRemovedFromCode_SaysWhyAndReadsTheFlagAgain", async () => {
    const reads = { count: 0 };
    server.use(
      meAnswer(),
      http.get("*/v1/platform/feature-flags/:key", () => {
        reads.count += 1;
        return HttpResponse.json(bulkEditDetail);
      }),
      previewAnswer(8),
      http.put("*/v1/platform/feature-flags/:key/everyone-else", () =>
        problem(409, ErrorCode.FeatureFlagRemovedFromCode),
      ),
    );
    renderWithProviders(<FlagPage flagKey="roles.bulk_edit" />);
    await userEvent.click(
      (await rules()).getByRole("button", { name: "Turn on for everyone" }),
    );
    const dialog = within(await screen.findByRole("alertdialog"));
    await dialog.findByText(/8 more organisations get it now/);

    await userEvent.click(
      dialog.getByRole("button", { name: "Turn on for everyone" }),
    );

    expect(
      await dialog.findByText(sentence("roles.bulk_edit did not change")),
    ).toBeVisible();
    expect(
      dialog.getByText(
        /This flag was removed from the code, so its rules no longer change\./,
      ),
    ).toBeVisible();
    await vi.waitFor(() => expect(reads.count).toBe(2));
  });

  it("FlagPage_KillSwitchTurnOff_AsksInRedWithTheCount", async () => {
    server.use(
      meAnswer(),
      flagAnswer({ ...emailsOffDetail, flag: emailsSend, history: [] }),
      previewAnswer(10),
    );
    renderWithProviders(<FlagPage flagKey="emails.send" />);

    await userEvent.click(
      (await rules()).getByRole("button", { name: "Turn off for everyone" }),
    );

    const dialog = within(await screen.findByRole("alertdialog"));
    expect(
      dialog.getByText(
        sentence("Turn emails.send off for everyone in Production?"),
      ),
    ).toBeVisible();
    expect(
      await dialog.findByText(
        "10 organisations lose it until you turn it back on.",
      ),
    ).toBeVisible();
    expect(
      dialog.getByRole("button", { name: "Turn off for everyone" }).className,
    ).toMatch(/bg-destructive/);
  });
});
