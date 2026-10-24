import {
  type AuditActorResponse,
  type AuditDetailsResponse,
  type AuditEntryResponse,
  AuditOperation,
  type AuditTargetResponse,
  TenantOwnerMadeDetailsResponseOperation,
  TenantPurgeWarnedDetailsResponseOperation,
  TenantRenamedDetailsResponseOperation,
  TenantSuspendedDetailsResponseOperation,
} from "@repo/api-client";
import { screen } from "@testing-library/react";
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
import { messages } from "@/i18n/messages";
import {
  ciara,
  deletedActor,
  deletedUser,
  entry,
  john,
  mehmet,
  tenantTarget,
  thePlatform,
} from "../../test/activity";
import {
  atlas,
  corrib,
  kestrel,
  liffey,
  meAnswer,
  northwind,
} from "../../test/organisations";
import { renderWithProviders } from "../../test/render";
import { ActivitySentence } from "./activity-sentence";

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
beforeEach(() => server.use(meAnswer()));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

/** The operations of the platform's log (the API's catalog, column Audit, P). */
const platformOperations = [
  AuditOperation.TenantCreated,
  AuditOperation.TenantRenamed,
  AuditOperation.TenantSuspended,
  AuditOperation.TenantReactivated,
  AuditOperation.TenantClosed,
  AuditOperation.TenantReopened,
  AuditOperation.TenantOwnerInvited,
  AuditOperation.TenantOwnerMade,
  AuditOperation.TenantPurgeWarned,
  AuditOperation.TenantPurged,
  AuditOperation.UserSuspended,
  AuditOperation.UserReactivated,
  AuditOperation.UserRenamed,
  AuditOperation.UserDeleted,
  AuditOperation.UserPreferencesChanged,
  AuditOperation.SystemAdminGranted,
  AuditOperation.SystemAdminRevoked,
];

/** The sentence of the operation in a message file, found by its path: `activity.operations.tenant.created`. */
const sentenceIn = (language: keyof typeof messages, operation: string) =>
  `activity.operations.${operation}`
    .split(".")
    .reduce<unknown>(
      (node, key) => (node as Record<string, unknown> | undefined)?.[key],
      messages[language],
    );

const suspended: AuditDetailsResponse = {
  operation: TenantSuspendedDetailsResponseOperation.TenantSuspended,
  reason: "Contract under review",
};
const renamed: AuditDetailsResponse = {
  operation: TenantRenamedDetailsResponseOperation.TenantRenamed,
  oldName: "Northwind",
  newName: "Northwind Traders",
};
const ownerMade: AuditDetailsResponse = {
  operation: TenantOwnerMadeDetailsResponseOperation.TenantOwnerMade,
  userId: "0198f0a2-0000-7000-8000-0000000000d2",
  oldRole: {
    id: "0198f0a2-0000-7000-8000-0000000000f1",
    name: "member",
    builtIn: true,
  },
};
const purgeWarned: AuditDetailsResponse = {
  operation: TenantPurgeWarnedDetailsResponseOperation.TenantPurgeWarned,
  purgesAt: "2026-10-22T09:00:00Z",
};

/** The paragraph of the sentence: the names inside it are elements of their own. */
const findSentence = (text: string) =>
  screen.findByText((_, element) => element?.textContent === text, {
    selector: "p",
  });

const renderEntry = (
  value: AuditEntryResponse,
  locale: keyof typeof messages = "en",
) => renderWithProviders(<ActivitySentence entry={value} />, locale);

type Case = [
  string,
  AuditOperation,
  AuditActorResponse,
  AuditTargetResponse,
  AuditDetailsResponse | null,
  string,
];

// The Activity board's sentences, then the operations the board does not draw.
const cases: Case[] = [
  [
    "TenantCreated",
    AuditOperation.TenantCreated,
    ciara,
    tenantTarget(liffey),
    null,
    "Ciara Byrne created Liffey Design and invited its Owner.",
  ],
  [
    "TenantSuspended",
    AuditOperation.TenantSuspended,
    ciara,
    tenantTarget(atlas),
    suspended,
    "Ciara Byrne suspended Atlas Logistics. Reason: Contract under review.",
  ],
  [
    "TenantRenamed",
    AuditOperation.TenantRenamed,
    ciara,
    tenantTarget(),
    renamed,
    "Ciara Byrne renamed Northwind to Northwind Traders.",
  ],
  [
    "TenantReactivated",
    AuditOperation.TenantReactivated,
    ciara,
    tenantTarget(),
    null,
    "Ciara Byrne reactivated Northwind Traders.",
  ],
  [
    "TenantClosed",
    AuditOperation.TenantClosed,
    ciara,
    tenantTarget(),
    null,
    "Ciara Byrne closed Northwind Traders.",
  ],
  [
    "TenantReopened",
    AuditOperation.TenantReopened,
    ciara,
    tenantTarget(),
    null,
    "Ciara Byrne reopened Northwind Traders.",
  ],
  [
    "TenantOwnerInvited",
    AuditOperation.TenantOwnerInvited,
    ciara,
    tenantTarget(),
    null,
    "Ciara Byrne invited a new Owner of Northwind Traders.",
  ],
  [
    "TenantOwnerMade",
    AuditOperation.TenantOwnerMade,
    ciara,
    tenantTarget(),
    ownerMade,
    "Ciara Byrne made a member an Owner of Northwind Traders.",
  ],
  [
    "TenantPurgeWarned",
    AuditOperation.TenantPurgeWarned,
    thePlatform,
    tenantTarget(kestrel),
    purgeWarned,
    "The Owners of Kestrel Labs were warned that it will be deleted on 22 Oct 2026.",
  ],
  [
    "TenantPurged",
    AuditOperation.TenantPurged,
    thePlatform,
    tenantTarget(corrib),
    null,
    "Corrib Press was deleted.",
  ],
  [
    "SystemAdminGranted",
    AuditOperation.SystemAdminGranted,
    ciara,
    mehmet,
    null,
    "Ciara Byrne made Mehmet Kaya a system admin.",
  ],
  [
    "SystemAdminRevoked",
    AuditOperation.SystemAdminRevoked,
    ciara,
    mehmet,
    null,
    "Ciara Byrne removed Mehmet Kaya as a system admin.",
  ],
  [
    "UserSuspended",
    AuditOperation.UserSuspended,
    ciara,
    john,
    null,
    "Ciara Byrne suspended the user John Byrne.",
  ],
  [
    "UserReactivated",
    AuditOperation.UserReactivated,
    ciara,
    john,
    null,
    "Ciara Byrne reactivated the user John Byrne.",
  ],
  [
    "UserRenamed",
    AuditOperation.UserRenamed,
    ciara,
    john,
    null,
    "Ciara Byrne renamed the user John Byrne.",
  ],
  [
    "UserDeleted",
    AuditOperation.UserDeleted,
    ciara,
    deletedUser,
    null,
    "Ciara Byrne deleted a user.",
  ],
  [
    "UserDeletedByThePurge",
    AuditOperation.UserDeleted,
    thePlatform,
    deletedUser,
    null,
    "A user was deleted.",
  ],
  [
    "UserPreferencesChanged",
    AuditOperation.UserPreferencesChanged,
    { ...ciara, userId: john.id, displayName: "John Byrne" },
    john,
    null,
    "John Byrne changed their preferences.",
  ],
  [
    "UserRenamedThemselves",
    AuditOperation.UserRenamed,
    { ...ciara, userId: john.id, displayName: "John Byrne" },
    john,
    null,
    "John Byrne changed their name.",
  ],
  [
    "DeletedActor",
    AuditOperation.TenantReactivated,
    deletedActor,
    tenantTarget(),
    null,
    "Deleted user reactivated Northwind Traders.",
  ],
  [
    "DeletedTarget",
    AuditOperation.UserSuspended,
    ciara,
    deletedUser,
    null,
    "Ciara Byrne suspended the user Deleted user.",
  ],
  [
    "UnnamedActor",
    AuditOperation.TenantClosed,
    { ...ciara, displayName: null },
    tenantTarget(),
    null,
    "Unnamed user closed Northwind Traders.",
  ],
];

describe("ActivitySentence", () => {
  // A new operation of the platform's log fails here until each language has its sentence.
  it.each(Object.keys(messages) as (keyof typeof messages)[])(
    "Messages_EveryPlatformOperationIn%s_HasASentence",
    (language) => {
      expect(
        platformOperations.filter(
          (operation) => typeof sentenceIn(language, operation) !== "string",
        ),
      ).toEqual([]);
    },
  );

  it.each(cases)(
    "ActivitySentence_%s_SaysTheBoardsSentence",
    async (_, operation, actor, target, details, text) => {
      renderEntry(entry({ operation, actor, target, details }));

      expect(await findSentence(text)).toBeInTheDocument();
    },
  );

  it.each([
    ["en", "22 Oct 2026"],
    ["tr", "22 Eki 2026"],
    ["ar", "22 أكتوبر 2026"],
  ] as const)(
    "ActivitySentence_PurgeWarnedIn%s_NamesTheDayOfThePurge",
    async (locale, date) => {
      renderEntry(
        entry({
          operation: AuditOperation.TenantPurgeWarned,
          actor: thePlatform,
          target: tenantTarget(kestrel),
          details: purgeWarned,
        }),
        locale,
      );

      expect(
        await screen.findByText(date, { exact: false, selector: "p" }),
      ).toBeInTheDocument();
    },
  );

  it.each([
    [
      "tr",
      "Ciara Byrne, Atlas Logistics kuruluşunu askıya aldı. Neden: Contract under review.",
    ],
    ["ar", "أوقف Ciara Byrne Atlas Logistics. السبب: Contract under review."],
  ] as const)(
    "ActivitySentence_SuspendedIn%s_GivesTheReason",
    async (locale, text) => {
      renderEntry(
        entry({
          operation: AuditOperation.TenantSuspended,
          target: tenantTarget(atlas),
          details: suspended,
        }),
        locale,
      );

      expect(await findSentence(text)).toBeInTheDocument();
    },
  );

  it("ActivitySentence_OperationThePlatformDoesNotKnow_SaysANeutralSentenceWithItsName", async () => {
    renderEntry(entry({ operation: "tenant.archived" as AuditOperation }));

    expect(
      await findSentence("Ciara Byrne made a change (tenant.archived)."),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_OperationOfTheTenantLog_SaysANeutralSentenceWithItsName", async () => {
    renderEntry(entry({ operation: AuditOperation.MemberJoined }));

    expect(
      await findSentence("Ciara Byrne made a change (member.joined)."),
    ).toBeInTheDocument();
  });

  it("ActivitySentence_TargetOrganisation_LinksToItsPage", async () => {
    renderEntry(
      entry({
        operation: AuditOperation.TenantReactivated,
        target: tenantTarget(),
      }),
    );

    expect(
      await screen.findByRole("link", { name: "Northwind Traders" }),
    ).toHaveAttribute("href", `/organisations/${northwind.id}`);
  });

  it("ActivitySentence_TargetUser_LinksToTheUsersPage", async () => {
    renderEntry(entry());

    expect(
      await screen.findByRole("link", { name: "John Byrne" }),
    ).toHaveAttribute("href", `/users/${john.id}`);
  });

  it("ActivitySentence_DeletedTarget_DoesNotLink", async () => {
    renderEntry(entry({ target: deletedUser }));

    expect(await screen.findByText("Deleted user")).toBeInTheDocument();
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });

  it("ActivitySentence_OwnerMade_LinksTheMemberToTheUsersPage", async () => {
    renderEntry(
      entry({
        operation: AuditOperation.TenantOwnerMade,
        target: tenantTarget(),
        details: ownerMade,
      }),
    );

    expect(
      await screen.findByRole("link", { name: "a member" }),
    ).toHaveAttribute("href", "/users/0198f0a2-0000-7000-8000-0000000000d2");
  });

  it("ActivitySentence_InArabic_IsolatesTheLatinNames", async () => {
    const { container } = renderEntry(
      entry({ operation: AuditOperation.SystemAdminGranted, target: mehmet }),
      "ar",
    );

    expect(
      await findSentence("جعل Ciara Byrne Mehmet Kaya مسؤول نظام."),
    ).toBeInTheDocument();
    expect(
      [...container.querySelectorAll("bdi")].map((name) => name.textContent),
    ).toEqual(["Ciara Byrne", "Mehmet Kaya"]);
  });
});
