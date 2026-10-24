import { AuditOperation, ErrorCode } from "@repo/api-client";
import { screen } from "@testing-library/react";
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
  detailAnswer,
  detailOf,
  inDays,
  kestrel,
  liffey,
  meAnswer,
  moyne,
  northwind,
  problem,
} from "../../../test/organisations";
import { renderWithProviders } from "../../../test/render";
import { OrganisationPage } from "./organisation-page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() =>
  window.history.replaceState(null, "", `/organisations/${northwind.id}`),
);
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe("OrganisationPage", () => {
  it("OrganisationPage_Active_ShowsItsOwnersMembersAndActions", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(northwind)));

    renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

    expect(
      await screen.findByRole("heading", { name: "Northwind Traders" }),
    ).toBeVisible();
    expect(screen.getByText("northwind")).toBeVisible();
    expect(screen.getByText("Ayşe Yılmaz")).toBeVisible();
    expect(screen.getByText("ayse@northwind.example")).toBeVisible();
    expect(screen.getByText("7")).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Actions for Northwind Traders" }),
    ).toBeVisible();
    expect(screen.getByRole("tab", { name: "Overview" })).toBeVisible();
    expect(screen.getByRole("tab", { name: "Activity" })).toBeVisible();
  });

  it("OrganisationPage_WaitingForAPendingOwner_SaysUntilWhenTheLinkWorks", async () => {
    const invitation = liffey.ownerInvitation;
    server.use(meAnswer(), detailAnswer(detailOf(liffey)));

    renderWithProviders(<OrganisationPage tenantId={liffey.id} />);

    expect(await screen.findByText("Waiting for the Owner.")).toBeVisible();
    expect(
      screen.getByText(
        `works until ${dateOf(invitation?.expiresAt as string)}. Nobody can sign in until the Owner joins.`,
        { exact: false },
      ),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Invite Owner" })).toBeVisible();
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

  it("OrganisationPage_WaitingWithoutAnInvitation_LinksTheHintToTheUsersList", async () => {
    server.use(
      meAnswer(),
      detailAnswer(detailOf({ ...liffey, ownerInvitation: null })),
    );

    renderWithProviders(<OrganisationPage tenantId={liffey.id} />);

    expect(
      await screen.findByRole("link", { name: "open their user page" }),
    ).toHaveAttribute("href", "/users");
  });

  it("OrganisationPage_WaitingAfterAnExpiredInvitation_LinksTheHintToTheInvitedEmail", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(burren)));

    renderWithProviders(<OrganisationPage tenantId={burren.id} />);

    expect(
      await screen.findByRole("link", { name: "open their user page" }),
    ).toHaveAttribute("href", "/users?q=orla%40burren.example");
  });

  it("OrganisationPage_WaitingAfterAnExpiredInvitation_SaysItExpired", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(burren)));

    renderWithProviders(<OrganisationPage tenantId={burren.id} />);

    expect(
      await screen.findByText(
        `expired on ${dateOf(burren.ownerInvitation?.expiresAt as string)}. Nobody can sign in until an Owner joins.`,
        { exact: false },
      ),
    ).toBeVisible();
    expect(screen.getByText("The invitation expired.")).toBeVisible();
    expect(screen.getByRole("button", { name: "Invite Owner" })).toBeVisible();
  });

  it("OrganisationPage_Suspended_ShowsTheReason", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(atlas)));

    renderWithProviders(<OrganisationPage tenantId={atlas.id} />);

    expect(
      await screen.findByText("Suspended. Members cannot sign in."),
    ).toBeVisible();
    expect(
      screen.getByText(
        "Reason: Contract under review. Members do not see this reason.",
      ),
    ).toBeVisible();
  });

  it("OrganisationPage_ClosedAndTheWarningAhead_SaysWhenTheOwnersAreWarned", async () => {
    const warning = inDays(1);
    server.use(
      meAnswer(),
      detailAnswer(detailOf(kestrel, { purgeWarningAt: warning })),
    );

    renderWithProviders(<OrganisationPage tenantId={kestrel.id} />);

    expect(
      await screen.findByText(
        `Closed on ${dateOf(kestrel.closedAt as string)}. You can reopen it until ${dateOf(kestrel.reopenableUntil as string)}. After that it is deleted.`,
      ),
    ).toBeVisible();
    expect(
      screen.getByText(`The Owners are warned on ${dateOf(warning)}.`),
    ).toBeVisible();
    expect(screen.getByRole("button", { name: "Reopen" })).toBeVisible();
  });

  it("OrganisationPage_ClosedAndTheWarningPast_SaysWhenTheOwnersWereWarned", async () => {
    const warning = inDays(-1);
    server.use(
      meAnswer(),
      detailAnswer(detailOf(kestrel, { purgeWarningAt: warning })),
    );

    renderWithProviders(<OrganisationPage tenantId={kestrel.id} />);

    expect(
      await screen.findByText(`The Owners were warned on ${dateOf(warning)}.`),
    ).toBeVisible();
  });

  it("OrganisationPage_ClosedWithoutAWarning_SaysNothingOfIt", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(kestrel)));

    renderWithProviders(<OrganisationPage tenantId={kestrel.id} />);

    expect(await screen.findByRole("button", { name: "Reopen" })).toBeVisible();
    expect(screen.queryByText(/warned/)).toBeNull();
  });

  it("OrganisationPage_BeingDeleted_HasNoReopenAndNoActions", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(moyne)));

    renderWithProviders(<OrganisationPage tenantId={moyne.id} />);

    expect(
      await screen.findByText(
        "Moyne Gallery is being deleted and can’t be reopened.",
      ),
    ).toBeVisible();
    expect(screen.getByText("Being deleted")).toBeVisible();
    expect(screen.queryByRole("button", { name: "Reopen" })).toBeNull();
    expect(
      screen.queryByRole("button", { name: "Actions for Moyne Gallery" }),
    ).toBeNull();
  });

  it("OrganisationPage_Deleted_ShowsTheTombstone", async () => {
    server.use(meAnswer(), detailAnswer(detailOf(corrib)));

    renderWithProviders(<OrganisationPage tenantId={corrib.id} />);

    expect(
      await screen.findByText(
        `Deleted on ${dateOf(corrib.purgedAt as string)}.`,
      ),
    ).toBeVisible();
    expect(
      screen.getByText(
        "Its members, invitations and data are gone. Activity keeps what happened.",
      ),
    ).toBeVisible();
    expect(screen.getByText("corrib-press")).toBeVisible();
    expect(screen.getByText("Deleted")).toBeVisible();
    expect(screen.queryByText("Members")).toBeNull();
    expect(screen.queryByText("Owner")).toBeNull();
    expect(
      screen.queryByRole("button", { name: "Actions for Corrib Press" }),
    ).toBeNull();
  });

  it("OrganisationPage_UnknownTenant_ShowsPageNotFound", async () => {
    server.use(
      meAnswer(),
      http.get(`*/v1/platform/tenants/${northwind.id}`, () =>
        problem(404, ErrorCode.TenantNotFound),
      ),
    );

    renderWithProviders(<OrganisationPage tenantId={northwind.id} />);

    expect(await screen.findByText("Page not found")).toBeVisible();
  });

  it("OrganisationPage_IdThatIsNoUuid_ShowsPageNotFoundWithoutACall", async () => {
    server.use(meAnswer());

    renderWithProviders(<OrganisationPage tenantId="northwind" />);

    expect(await screen.findByText("Page not found")).toBeVisible();
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

    await userEvent.click(await screen.findByRole("tab", { name: "Activity" }));

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
    expect(screen.getByRole("tab", { name: "Activity" })).toHaveAttribute(
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

    await userEvent.click(await screen.findByRole("tab", { name: "Activity" }));

    expect(await screen.findByRole("listitem")).toBeVisible();
    expect(requests.map((url) => url.searchParams.get("tenantId"))).toEqual([
      corrib.id,
    ]);
  });
});
