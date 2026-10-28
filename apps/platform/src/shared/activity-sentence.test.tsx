import {
  type AuditActorResponse,
  type AuditDetailsResponse,
  type AuditEntryResponse,
  AuditOperation,
  type AuditTargetResponse,
  AuditTargetResponseKind,
  FeatureKey,
  LimitKey,
  PlanCreatedDetailsResponseOperation,
  type PlanUpdatedDetailsResponse,
  PlanUpdatedDetailsResponseOperation,
  TenantCreatedDetailsResponseOperation,
  TenantEntitlementOverrideRemovedDetailsResponseOperation,
  TenantEntitlementOverrideSetDetailsResponseOperation,
  TenantOwnerMadeDetailsResponseOperation,
  TenantPlanChangedDetailsResponseOperation,
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
import { free, partner, pro } from "../../test/plans";
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
  AuditOperation.PlanCreated,
  AuditOperation.PlanUpdated,
  AuditOperation.TenantPlanChanged,
  AuditOperation.TenantEntitlementOverrideSet,
  AuditOperation.TenantEntitlementOverrideRemoved,
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
  displayName: null,
  deleted: false,
  oldRole: {
    id: "0198f0a2-0000-7000-8000-0000000000f1",
    name: "member",
    builtIn: true,
  },
};
/** Make Owner with the new Owner's name, as the read side resolves it (the API's step r13). */
const ownerMadeNamed: AuditDetailsResponse = {
  ...ownerMade,
  userId: mehmet.id,
  displayName: "Mehmet Kaya",
};
const ownerMadeDeleted: AuditDetailsResponse = {
  ...ownerMade,
  deleted: true,
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
    ownerMadeNamed,
    "Ciara Byrne made Mehmet Kaya an Owner of Northwind Traders.",
  ],
  [
    "TenantOwnerMadeDeletedOwner",
    AuditOperation.TenantOwnerMade,
    ciara,
    tenantTarget(),
    ownerMadeDeleted,
    "Ciara Byrne made Deleted user an Owner of Northwind Traders.",
  ],
  [
    "TenantOwnerMadeUnnamedOwner",
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

  it("ActivitySentence_OwnerMadeUnnamed_LinksTheMemberToTheUsersPage", async () => {
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

  it("ActivitySentence_OwnerMade_LinksTheNewOwnerToTheirPage", async () => {
    renderEntry(
      entry({
        operation: AuditOperation.TenantOwnerMade,
        target: tenantTarget(),
        details: ownerMadeNamed,
      }),
    );

    expect(
      await screen.findByRole("link", { name: "Mehmet Kaya" }),
    ).toHaveAttribute("href", `/users/${mehmet.id}`);
  });

  it("ActivitySentence_OwnerMadeDeletedOwner_DoesNotLinkTheOwner", async () => {
    renderEntry(
      entry({
        operation: AuditOperation.TenantOwnerMade,
        target: tenantTarget(),
        details: ownerMadeDeleted,
      }),
    );

    expect(await screen.findByText("Deleted user")).toBeInTheDocument();
    expect(screen.getAllByRole("link").map((link) => link.textContent)).toEqual(
      ["Northwind Traders"],
    );
  });

  it.each([
    [
      "tr",
      "Named",
      ownerMadeNamed,
      "Ciara Byrne, Mehmet Kaya kullanıcısını Northwind Traders kuruluşunda Sahip yaptı.",
    ],
    [
      "ar",
      "Named",
      ownerMadeNamed,
      "جعل Ciara Byrne Mehmet Kaya مالكًا في Northwind Traders.",
    ],
    [
      "tr",
      "Deleted",
      ownerMadeDeleted,
      "Ciara Byrne, silinmiş bir kullanıcıyı Northwind Traders kuruluşunda Sahip yaptı.",
    ],
    [
      "ar",
      "Deleted",
      ownerMadeDeleted,
      "جعل Ciara Byrne مستخدمًا محذوفًا مالكًا في Northwind Traders.",
    ],
  ] as const)(
    "ActivitySentence_OwnerMadeIn%s_Says%sNewOwner",
    async (locale, _, details, text) => {
      renderEntry(
        entry({
          operation: AuditOperation.TenantOwnerMade,
          target: tenantTarget(),
          details,
        }),
        locale,
      );

      expect(await findSentence(text)).toBeInTheDocument();
    },
  );

  it("ActivitySentence_Links_AreTextLinksUnderlinedInsideTheSentence", async () => {
    renderEntry(
      entry({
        operation: AuditOperation.TenantOwnerMade,
        target: tenantTarget(),
        details: ownerMadeNamed,
      }),
    );

    await screen.findByRole("link", { name: "Mehmet Kaya" });
    const links = screen.getAllByRole("link");
    expect(links.map((link) => link.textContent)).toEqual([
      "Mehmet Kaya",
      "Northwind Traders",
    ]);
    expect(
      links.filter((link) => !link.classList.contains("underline")),
    ).toEqual([]);
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

// The Plans boards' Activity sentences (the platform's Activity boards and the Messages board's "Plans" section).
const planTarget = (plan: {
  id: string;
  name: string;
}): AuditTargetResponse => ({
  kind: AuditTargetResponseKind.Plan,
  id: plan.id,
  name: plan.name,
  deleted: null,
});
const value = (
  values: Partial<{ enabled: boolean; maximum: number; unlimited: boolean }>,
) => ({ enabled: null, maximum: null, unlimited: false, ...values });
const proValues = {
  [FeatureKey.RolesCustom]: value({ enabled: true }),
  [LimitKey.RolesCustomMax]: value({ maximum: 20 }),
  [LimitKey.MembersMax]: value({ unlimited: true }),
};
const updated = (
  values: Partial<PlanUpdatedDetailsResponse>,
): AuditDetailsResponse => ({
  operation: PlanUpdatedDetailsResponseOperation.PlanUpdated,
  oldName: "Pro",
  newName: "Pro",
  wasDefault: false,
  isDefault: false,
  oldEntitlements: proValues,
  newEntitlements: proValues,
  previousDefaultPlan: null,
  ...values,
});
const burrenTarget = tenantTarget({
  id: "0198f0a2-0000-7000-8000-000000000002",
  name: "Burren Supply",
});

type PlanCase = [
  string,
  AuditOperation,
  AuditTargetResponse,
  AuditDetailsResponse,
  { en: string; tr: string; ar: string },
];

const planCases: PlanCase[] = [
  [
    "PlanCreated",
    AuditOperation.PlanCreated,
    planTarget(partner),
    {
      operation: PlanCreatedDetailsResponseOperation.PlanCreated,
      isDefault: false,
      entitlements: proValues,
    },
    {
      en: "Ciara Byrne created the plan Partner.",
      tr: "Ciara Byrne, Partner planını oluşturdu.",
      ar: "أنشأ Ciara Byrne الخطة Partner.",
    },
  ],
  [
    "PlanUpdatedOneValue",
    AuditOperation.PlanUpdated,
    planTarget(pro),
    updated({
      newEntitlements: {
        ...proValues,
        [LimitKey.RolesCustomMax]: value({ maximum: 10 }),
      },
    }),
    {
      en: "Ciara Byrne changed the plan Pro: Custom roles limit from 20 to 10.",
      tr: "Ciara Byrne, Pro planını değiştirdi: Özel rol sınırı, 20 değerinden 10 değerine.",
      ar: "غيّر Ciara Byrne الخطة Pro: حد الأدوار المخصصة من 20 إلى 10.",
    },
  ],
  [
    "PlanUpdatedSeveralValues",
    AuditOperation.PlanUpdated,
    planTarget(partner),
    updated({
      oldName: "Partners",
      newName: "Partner",
      oldEntitlements: {
        ...proValues,
        [LimitKey.RolesCustomMax]: value({ maximum: 50 }),
      },
      newEntitlements: {
        ...proValues,
        [LimitKey.RolesCustomMax]: value({ maximum: 40 }),
        [LimitKey.MembersMax]: value({ maximum: 100 }),
      },
    }),
    {
      en: "Ciara Byrne changed several values of the plan Partner: Custom roles limit from 50 to 40; Members limit from unlimited to 100; name from Partners to Partner.",
      tr: "Ciara Byrne, Partner planında birkaç değeri değiştirdi: Özel rol sınırı, 50 değerinden 40 değerine; Üye sınırı, sınırsız değerinden 100 değerine; ad, Partners değerinden Partner değerine.",
      ar: "غيّر Ciara Byrne عدة قيم في الخطة Partner: حد الأدوار المخصصة من 50 إلى 40؛ حد الأعضاء من غير محدود إلى 100؛ الاسم من Partners إلى Partner.",
    },
  ],
  [
    "PlanUpdatedFeatureAndUnset",
    AuditOperation.PlanUpdated,
    planTarget(pro),
    updated({
      newEntitlements: {
        [FeatureKey.RolesCustom]: value({ enabled: false }),
        [LimitKey.RolesCustomMax]: value({ maximum: 20 }),
      },
    }),
    {
      en: "Ciara Byrne changed several values of the plan Pro: Custom roles from on to off; Members limit from unlimited to not set.",
      tr: "Ciara Byrne, Pro planında birkaç değeri değiştirdi: Özel roller, açık değerinden kapalı değerine; Üye sınırı, sınırsız değerinden ayarlanmadı değerine.",
      ar: "غيّر Ciara Byrne عدة قيم في الخطة Pro: الأدوار المخصصة من مفعّل إلى متوقف؛ حد الأعضاء من غير محدود إلى غير محدد.",
    },
  ],
  [
    "PlanRenamed",
    AuditOperation.PlanUpdated,
    planTarget(partner),
    updated({ oldName: "Partners", newName: "Partner" }),
    {
      en: "Ciara Byrne renamed the plan Partners to Partner.",
      tr: "Ciara Byrne, Partners planının adını Partner olarak değiştirdi.",
      ar: "غيّر Ciara Byrne اسم الخطة Partners إلى Partner.",
    },
  ],
  [
    "PlanMadeDefault",
    AuditOperation.PlanUpdated,
    planTarget(free),
    updated({
      oldName: "Free",
      newName: "Free",
      isDefault: true,
      previousDefaultPlan: { id: pro.id, name: "Pro" },
    }),
    {
      en: "Ciara Byrne made Free the default plan, in place of Pro.",
      tr: "Ciara Byrne, Pro yerine Free planını varsayılan yaptı.",
      ar: "جعل Ciara Byrne Free الخطة الافتراضية بدلًا من Pro.",
    },
  ],
  [
    "TenantCreatedOnAPlan",
    AuditOperation.TenantCreated,
    tenantTarget(liffey),
    {
      operation: TenantCreatedDetailsResponseOperation.TenantCreated,
      plan: { id: free.id, name: "Free" },
    },
    {
      en: "Ciara Byrne created Liffey Design on Free and invited its Owner.",
      tr: "Ciara Byrne, Liffey Design kuruluşunu Free planında oluşturdu ve Sahibini davet etti.",
      ar: "أنشأ Ciara Byrne Liffey Design على Free ودعا مالكها.",
    },
  ],
  [
    "TenantPlanChanged",
    AuditOperation.TenantPlanChanged,
    tenantTarget({
      id: "0198f0a2-0000-7000-8000-000000000003",
      name: "Shannon Studio",
    }),
    {
      operation: TenantPlanChangedDetailsResponseOperation.TenantPlanChanged,
      oldPlan: { id: free.id, name: "Free" },
      newPlan: { id: pro.id, name: "Pro" },
    },
    {
      en: "Ciara Byrne moved Shannon Studio from Free to Pro.",
      tr: "Ciara Byrne, Shannon Studio kuruluşunu Free planından Pro planına geçirdi.",
      ar: "نقل Ciara Byrne Shannon Studio من Free إلى Pro.",
    },
  ],
  [
    "OverrideSet",
    AuditOperation.TenantEntitlementOverrideSet,
    burrenTarget,
    {
      operation:
        TenantEntitlementOverrideSetDetailsResponseOperation.TenantEntitlementOverrideSet,
      key: LimitKey.RolesCustomMax,
      oldValue: null,
      oldExpiresAt: null,
      newValue: value({ maximum: 25 }),
      newExpiresAt: "2026-12-31T00:00:00Z",
      reason: "Moving their old roles over; review at year end.",
    },
    {
      en: "Ciara Byrne set Burren Supply’s Custom roles limit to 25 until 31 Dec 2026 (before: none). Reason: Moving their old roles over; review at year end.",
      tr: "Ciara Byrne, Burren Supply kuruluşunun Özel rol sınırı değerini 31 Ara 2026 tarihine kadar 25 yaptı (önceki: yok). Neden: Moving their old roles over; review at year end.",
      ar: "جعل Ciara Byrne حد الأدوار المخصصة لـBurren Supply 25 حتى 31 ديسمبر 2026 (قبل ذلك: لا شيء). السبب: Moving their old roles over; review at year end.",
    },
  ],
  [
    "OverrideSetNoExpiry",
    AuditOperation.TenantEntitlementOverrideSet,
    burrenTarget,
    {
      operation:
        TenantEntitlementOverrideSetDetailsResponseOperation.TenantEntitlementOverrideSet,
      key: FeatureKey.RolesCustom,
      oldValue: value({ enabled: true }),
      oldExpiresAt: null,
      newValue: value({ enabled: false }),
      newExpiresAt: null,
      reason: "Under review",
    },
    {
      en: "Ciara Byrne set Burren Supply’s Custom roles to off, no expiry (before: on). Reason: Under review.",
      tr: "Ciara Byrne, Burren Supply kuruluşunun Özel roller değerini süresiz olarak kapalı yaptı (önceki: açık). Neden: Under review.",
      ar: "جعل Ciara Byrne الأدوار المخصصة لـBurren Supply متوقف بلا انتهاء (قبل ذلك: مفعّل). السبب: Under review.",
    },
  ],
  [
    "OverrideRemoved",
    AuditOperation.TenantEntitlementOverrideRemoved,
    tenantTarget(atlas),
    {
      operation:
        TenantEntitlementOverrideRemovedDetailsResponseOperation.TenantEntitlementOverrideRemoved,
      key: LimitKey.MembersMax,
      oldValue: value({ maximum: 40 }),
      oldExpiresAt: "2026-11-30T00:00:00Z",
    },
    {
      en: "Ciara Byrne removed Atlas Logistics’s override on Members limit (40, until 30 Nov 2026).",
      tr: "Ciara Byrne, Atlas Logistics kuruluşunun Üye sınırı istisnasını kaldırdı (40, 30 Kas 2026 tarihine kadar).",
      ar: "أزال Ciara Byrne استثناء Atlas Logistics على حد الأعضاء (40، حتى 30 نوفمبر 2026).",
    },
  ],
  [
    "OverrideRemovedNoExpiry",
    AuditOperation.TenantEntitlementOverrideRemoved,
    tenantTarget(atlas),
    {
      operation:
        TenantEntitlementOverrideRemovedDetailsResponseOperation.TenantEntitlementOverrideRemoved,
      key: LimitKey.MembersMax,
      oldValue: value({ unlimited: true }),
      oldExpiresAt: null,
    },
    {
      en: "Ciara Byrne removed Atlas Logistics’s override on Members limit (unlimited, no expiry).",
      tr: "Ciara Byrne, Atlas Logistics kuruluşunun Üye sınırı istisnasını kaldırdı (sınırsız, süresiz).",
      ar: "أزال Ciara Byrne استثناء Atlas Logistics على حد الأعضاء (غير محدود، بلا انتهاء).",
    },
  ],
];

describe("ActivitySentence of the plans", () => {
  it.each(
    planCases.flatMap(([name, operation, target, details, texts]) =>
      (["en", "tr", "ar"] as const).map(
        (locale) =>
          [
            `${name}In${locale}`,
            operation,
            target,
            details,
            locale,
            texts[locale],
          ] as const,
      ),
    ),
  )(
    "ActivitySentence_%s_SaysTheBoardsSentence",
    async (_, operation, target, details, locale, text) => {
      renderEntry(entry({ operation, target, details }), locale);

      expect(await findSentence(text)).toBeInTheDocument();
    },
  );

  it("ActivitySentence_PlanTarget_LinksToThePlansPage", async () => {
    renderEntry(
      entry({
        operation: AuditOperation.PlanCreated,
        target: planTarget(partner),
        details: planCases[0]?.[3] ?? null,
      }),
    );

    expect(
      await screen.findByRole("link", { name: "Partner" }),
    ).toHaveAttribute("href", `/settings/plans/${partner.id}`);
  });

  it("ActivitySentence_PlanChanged_LinksTheOrganisationAndBothPlansByTheirNamesThen", async () => {
    renderEntry(
      entry({
        operation: AuditOperation.TenantPlanChanged,
        target: tenantTarget(northwind),
        details: {
          operation:
            TenantPlanChangedDetailsResponseOperation.TenantPlanChanged,
          oldPlan: { id: free.id, name: "Starter" },
          newPlan: { id: pro.id, name: "Pro" },
        },
      }),
    );

    await screen.findByRole("link", { name: "Northwind Traders" });
    expect(
      screen
        .getAllByRole("link")
        .map((link) => [link.textContent, link.getAttribute("href")]),
    ).toEqual([
      ["Northwind Traders", `/organisations/${northwind.id}`],
      ["Starter", `/settings/plans/${free.id}`],
      ["Pro", `/settings/plans/${pro.id}`],
    ]);
  });

  it("ActivitySentence_OverrideSetInTheTenantLog_HasNoReason", async () => {
    renderEntry(
      entry({
        operation: AuditOperation.TenantEntitlementOverrideSet,
        target: burrenTarget,
        details: {
          operation:
            TenantEntitlementOverrideSetDetailsResponseOperation.TenantEntitlementOverrideSet,
          key: LimitKey.RolesCustomMax,
          oldValue: null,
          oldExpiresAt: null,
          newValue: value({ maximum: 25 }),
          newExpiresAt: null,
          reason: null,
        },
      }),
    );

    expect(
      await findSentence(
        "Ciara Byrne set Burren Supply’s Custom roles limit to 25, no expiry (before: none).",
      ),
    ).toBeInTheDocument();
  });
});
