import { AuditOperation, ErrorCode, LimitKey } from "@repo/api-client";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http } from "msw";
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
import { auditLogAnswer, entry, tenantTarget } from "../../../test/activity";
import {
  atlas,
  burren,
  corrib,
  dateOf,
  dateTimeOf,
  detailAnswer,
  detailOf,
  kestrel,
  liffey,
  meAnswer,
  moyne,
  northwind,
  problem,
} from "../../../test/organisations";
import {
  anyTenantPlanAnswer,
  burrenPlan,
  max,
  plansAnswer,
  tenantPlanAnswer,
} from "../../../test/plans";
import { renderWithProviders } from "../../../test/render";
import { OrganisationPage } from "./organisation-page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => {
  window.history.replaceState(null, "", `/organisations/${northwind.id}`);
  server.use(anyTenantPlanAnswer(), plansAnswer());
});
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

/** The value of a term of the details: a term has no accessible name, so the value is the definition at the term's place. */
const term = async (name: string) => {
  const terms = await screen.findAllByRole("term");
  const place = terms.findIndex((element) => element.textContent === name);
  return screen.getAllByRole("definition")[place] as HTMLElement;
};

const deniz = { name: "Deniz Kaya", deleted: false };

/** GetTenantPlan of the organisation with its seats: a limit of 15 with 12 of them used. */
const seatsOf = (tenantId: string) =>
  tenantPlanAnswer({
    ...burrenPlan({
      [LimitKey.MembersMax]: {
        planValue: max(15),
        effective: max(15),
        count: 12,
      },
    }),
    tenantId,
  });

describe("OrganisationPage", () => {
  it.each([
    ["en", "Created", "3 Mar 2026"],
    ["tr", "Oluşturulma", "3 Mar 2026"],
    ["ar", "تاريخ الإنشاء", "3 مارس 2026"],
  ] as const)(
    "OrganisationPage_CreatedIn%s_ShowsTheDateInTheDetails",
    async (locale, label, date) => {
      server.use(meAnswer(), detailAnswer(detailOf(northwind)));

      renderWithProviders(<OrganisationPage tenantId={northwind.id} />, locale);

      expect(await term(label)).toHaveTextContent(date);
    },
  );

  it("OrganisationPage_Header_HasTheWayBackTheStatusTheAddressAndTheMenu", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(northwind)));

    renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

    expect(
      await screen.findByRole("heading", { name: "Northwind Traders" }),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "Organisations" })).toHaveAttribute(
      "href",
      "/",
    );
    expect(
      screen.getAllByText("app.starter.example/northwind").length,
    ).toBeGreaterThan(0);
    expect(
      screen.getByRole("button", { name: "Actions for Northwind Traders" }),
    ).toBeVisible();
    expect(screen.getAllByRole("tab").map((tab) => tab.textContent)).toEqual([
      "Overview",
      "Plan",
      "Activity log",
    ]);
  });

  it("OrganisationPage_Overview_ShowsTheAddressTheStatusAndTheOwner", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(northwind)));

    renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

    const address = within(await term("Address"));
    expect(address.getByText("app.starter.example/northwind")).toBeVisible();
    expect(address.getByText("It never changes.")).toBeVisible();
    expect(address.getByRole("button", { name: /^Copy/ })).toBeVisible();
    expect(await term("Status")).toHaveTextContent("Active");
    const owner = within(await term("Owner"));
    expect(owner.getByText("Ayşe Yılmaz")).toBeVisible();
    expect(owner.getByText("ayse@northwind.example")).toBeVisible();
    expect(owner.getByRole("button", { name: /^Copy/ })).toBeVisible();
  });

  it("OrganisationPage_LimitedSeats_SaysTheMembersTheInvitationsAndTheSeats", async () => {
    server.use(
      meAnswer(),
      detailAnswer(
        detailOf(northwind, { memberCount: 10, pendingInvitationCount: 2 }),
      ),
      seatsOf(northwind.id),
    );

    renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

    const members = within(await term("Members"));
    expect(
      await members.findByText(
        "10 members and 2 pending invitations: 12 of 15 seats.",
      ),
    ).toBeVisible();
    expect(members.getByText("12 of 15 seats")).toBeVisible();
    expect(await term("Plan")).toHaveTextContent("Pro");
  });

  it.each([
    [7, 0, "7 members"],
    [0, 1, "0 members and 1 pending invitation"],
    [1, 3, "1 member and 3 pending invitations"],
  ])(
    "OrganisationPage_UnlimitedSeats_%iMembersAnd%iInvited_SaysOnlyTheCounts",
    async (memberCount, pendingInvitationCount, text) => {
      server.use(
        meAnswer(),
        detailAnswer(
          detailOf(northwind, { memberCount, pendingInvitationCount }),
        ),
      );

      renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

      expect(
        await within(await term("Members")).findByText(text),
      ).toBeVisible();
    },
  );

  it.each([
    [null, "3 Mar 2026"],
    [deniz, "3 Mar 2026, by Deniz Kaya"],
    [{ name: null, deleted: true }, "3 Mar 2026, by Deleted user"],
  ])(
    "OrganisationPage_CreatedBy%o_SaysWhoWhenItIsKnown",
    async (createdBy, text) => {
      server.use(meAnswer(), detailAnswer(detailOf(northwind, { createdBy })));

      renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

      await waitFor(async () =>
        expect(await term("Created")).toHaveTextContent(text),
      );
    },
  );

  it("OrganisationPage_WaitingForAPendingOwner_SaysUntilWhenTheLinkWorks", async () => {
    const invitation = liffey.ownerInvitation;
    server.use(meAnswer(), detailAnswer(detailOf(liffey)));

    renderWithProviders(<OrganisationPage tenantId={liffey.id} />);

    expect(await screen.findByText("Waiting for the Owner")).toBeVisible();
    expect(
      screen.getByText(
        `niamh@liffeydesign.example hasn’t accepted yet. The invitation works until ${dateOf(invitation?.expiresAt as string)}.`,
      ),
    ).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Invite Owner again" }),
    ).toBeVisible();
    const owner = within(await term("Owner"));
    expect(
      owner.getByText("Invited: niamh@liffeydesign.example"),
    ).toBeVisible();
    await waitFor(() =>
      expect(
        owner.getByText(`Sent ${dateTimeOf(invitation?.sentAt as string)}`),
      ).toBeVisible(),
    );
    expect(
      screen.getByText(
        (_, element) =>
          element?.tagName === "P" &&
          element.textContent ===
            "If the Owner joined as a member, open their user page and make them an Owner.",
      ),
    ).toBeVisible();
  });

  it("OrganisationPage_WaitingForAnOwner_LinksTheHintToTheUsersFoundByTheInvitedEmail", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(liffey)));

    renderWithProviders(<OrganisationPage tenantId={liffey.id} />);

    expect(
      await screen.findByRole("link", { name: "open their user page" }),
    ).toHaveAttribute("href", "/users?q=niamh%40liffeydesign.example");
  });

  it("OrganisationPage_WaitingWithoutAnInvitation_OffersInviteOwner", async () => {
    server.use(
      meAnswer(),
      detailAnswer(detailOf(liffey, { ownerInvitation: null })),
    );

    renderWithProviders(<OrganisationPage tenantId={liffey.id} />);

    expect(
      await screen.findByRole("button", { name: "Invite Owner" }),
    ).toBeVisible();
    expect(
      screen.getByRole("link", { name: "open their user page" }),
    ).toHaveAttribute("href", "/users");
  });

  it("OrganisationPage_WaitingAfterAnExpiredInvitation_SaysItExpired", async () => {
    const invitation = burren.ownerInvitation;
    server.use(meAnswer(), detailAnswer(detailOf(burren)));

    renderWithProviders(<OrganisationPage tenantId={burren.id} />);

    expect(
      await screen.findByText(
        `The invitation to orla@burren.example expired on ${dateOf(invitation?.expiresAt as string)}.`,
      ),
    ).toBeVisible();
    expect(
      screen.getByRole("link", { name: "open their user page" }),
    ).toHaveAttribute("href", "/users?q=orla%40burren.example");
  });

  it("OrganisationPage_Suspended_SaysWhenByWhomAndWhyAndReactivates", async () => {
    const suspendedAt = "2026-10-30T09:00:00Z";
    server.use(
      meAnswer(),
      detailAnswer(detailOf(atlas, { suspendedAt, suspendedBy: deniz })),
    );

    renderWithProviders(<OrganisationPage tenantId={atlas.id} />);

    expect(
      await screen.findByText(
        `Suspended on ${dateOf(suspendedAt)} by Deniz Kaya`,
      ),
    ).toBeVisible();
    expect(
      screen.getByText(
        "Reason: Contract under review. Its members can’t open it.",
      ),
    ).toBeVisible();
    await userEvent.click(screen.getByRole("button", { name: "Reactivate" }));
    expect(
      await screen.findByRole("alertdialog", {
        name: "Reactivate Atlas Logistics?",
      }),
    ).toBeVisible();
  });

  it("OrganisationPage_SuspendedBeforeItsDateWasKept_SaysSuspended", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(atlas)));

    renderWithProviders(<OrganisationPage tenantId={atlas.id} />);

    expect(
      await screen.findByText(
        "Reason: Contract under review. Its members can’t open it.",
      ),
    ).toBeVisible();
    expect(screen.getByText("Suspended", { selector: "p" })).toBeVisible();
  });

  it("OrganisationPage_Closed_SaysWhenByWhomAndUntilWhenAndReopens", async () => {
    server.use(
      meAnswer(),
      detailAnswer(detailOf(kestrel, { closedBy: deniz })),
    );

    renderWithProviders(<OrganisationPage tenantId={kestrel.id} />);

    expect(
      await screen.findByText(
        `Closed on ${dateOf(kestrel.closedAt as string)} by Deniz Kaya`,
      ),
    ).toBeVisible();
    expect(
      screen.getByText(
        `It can be reopened until ${dateOf(kestrel.reopenableUntil as string)}. Then it is deleted with all its data.`,
      ),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Reopen" })).toBeVisible();
  });

  it("OrganisationPage_BeingDeleted_SaysSinceWhenAndHasNoActionsNoPlanAndNoCounts", async () => {
    const purgeStartedAt = "2026-11-01T02:00:00Z";
    server.use(meAnswer(), detailAnswer(detailOf(moyne, { purgeStartedAt })));

    renderWithProviders(<OrganisationPage tenantId={moyne.id} />);

    expect(
      await screen.findByText(`Being deleted since ${dateOf(purgeStartedAt)}`),
    ).toBeVisible();
    expect(
      screen.getByText(
        "This can’t be stopped. Its data is being removed; the Activity log keeps its entries.",
      ),
    ).toBeVisible();
    expect(screen.queryByRole("button", { name: "Reopen" })).toBeNull();
    expect(
      screen.queryByRole("button", { name: "Actions for Moyne Gallery" }),
    ).toBeNull();
    expect(screen.getAllByRole("tab").map((tab) => tab.textContent)).toEqual([
      "Overview",
      "Activity log",
    ]);
    expect(await term("Owner")).toHaveTextContent("—");
    expect(await term("Members")).toHaveTextContent("—");
  });

  it("OrganisationPage_Deleted_ShowsTheTombstone", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(corrib)));

    renderWithProviders(<OrganisationPage tenantId={corrib.id} />);

    // The date is in the admin's zone once GetMe answers; before, the page writes it in the runtime's zone, a day apart near midnight.
    expect(
      await screen.findByText(
        `Deleted on ${dateOf(corrib.purgedAt as string)}`,
      ),
    ).toBeVisible();
    expect(
      screen.getByText(
        "Its data is gone. Its name and address stay reserved, and the Activity log keeps its entries.",
      ),
    ).toBeVisible();
    expect(await term("Created")).toHaveTextContent("4 Nov 2025");
    expect(await term("Owner")).toHaveTextContent("—");
    expect(await term("Plan")).toHaveTextContent("Pro");
    expect(screen.getAllByRole("tab").map((tab) => tab.textContent)).toEqual([
      "Overview",
      "Activity log",
    ]);
    expect(
      screen.queryByRole("button", { name: "Actions for Corrib Press" }),
    ).toBeNull();
  });

  it("OrganisationPage_PlanTab_ShowsThePlanAndItsKeys", async () => {
    window.history.replaceState(
      null,
      "",
      `/organisations/${northwind.id}?tab=plan`,
    );
    server.use(meAnswer(), detailAnswer(detailOf(northwind)));

    renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

    expect(
      await screen.findByRole("group", { name: "Custom roles limit" }),
    ).toBeVisible();
    expect(screen.getByRole("tab", { name: "Plan" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(screen.getByRole("button", { name: "Change plan" })).toBeVisible();
    expect(screen.queryByRole("term")).toBeNull();
  });

  it("OrganisationPage_UnknownTenant_ShowsOrganisationNotFound", async () => {
    server.use(
      meAnswer(),
      http.get(`*/v1/platform/tenants/${northwind.id}`, () =>
        problem(404, ErrorCode.TenantNotFound),
      ),
    );

    renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

    expect(await screen.findByText("Organisation not found")).toBeVisible();
  });

  it("OrganisationPage_IdThatIsNoUuid_ShowsOrganisationNotFoundWithoutACall", async () => {
    server.use(meAnswer());

    renderWithProviders(<OrganisationPage tenantId="northwind" />);

    expect(await screen.findByText("Organisation not found")).toBeVisible();
  });

  it("OrganisationPage_ActivityTab_ReadsThePlatformsEntriesAboutTheOrganisation", async () => {
    const requests: URL[] = [];
    server.use(
      meAnswer(),
      detailAnswer(detailOf(northwind)),
      auditLogAnswer(
        {
          "": {
            items: [
              entry({
                operation: AuditOperation.TenantReactivated,
                target: tenantTarget(),
              }),
            ],
            nextCursor: null,
          },
        },
        requests,
      ),
    );
    renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

    await userEvent.click(
      await screen.findByRole("tab", { name: "Activity log" }),
    );

    expect(
      await screen.findByText(
        (_, element) =>
          element?.tagName === "P" &&
          element.textContent === "Ciara Byrne reactivated Northwind Traders.",
      ),
    ).toBeVisible();
    expect(requests.map((url) => url.searchParams.get("tenantId"))).toEqual([
      northwind.id,
    ]);
    expect(new URLSearchParams(window.location.search).get("tab")).toBe(
      "activity",
    );
    expect(screen.queryByText("Members")).toBeNull();
  });

  it("OrganisationPage_ActivityInTheUrl_OpensTheActivityTab", async () => {
    window.history.replaceState(
      null,
      "",
      `/organisations/${northwind.id}?tab=activity`,
    );
    server.use(
      meAnswer(),
      detailAnswer(detailOf(northwind)),
      auditLogAnswer({ "": { items: [], nextCursor: null } }),
    );

    renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

    expect(
      await screen.findByRole("heading", { name: "No activity yet" }),
    ).toBeVisible();
    expect(
      screen.getByText("The platform’s entries about this organisation."),
    ).toBeVisible();
    expect(screen.getByRole("tab", { name: "Activity log" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
  });

  it("OrganisationPage_DeletedOrganisation_KeepsItsActivityTab", async () => {
    const requests: URL[] = [];
    server.use(
      meAnswer(),
      detailAnswer(detailOf(corrib)),
      auditLogAnswer(
        {
          "": {
            items: [
              entry({
                operation: AuditOperation.TenantPurged,
                target: tenantTarget(corrib),
              }),
            ],
            nextCursor: null,
          },
        },
        requests,
      ),
    );
    renderWithProviders(<OrganisationPage tenantId={corrib.id} />);

    await userEvent.click(
      await screen.findByRole("tab", { name: "Activity log" }),
    );

    expect(await screen.findByRole("listitem")).toBeVisible();
    expect(requests.map((url) => url.searchParams.get("tenantId"))).toEqual([
      corrib.id,
    ]);
  });
});
