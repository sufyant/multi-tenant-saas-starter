import type { Page, Route } from "@playwright/test";
import {
  AuditActorResponseKind,
  type AuditEntryResponse,
  AuditOperation,
  AuditTargetResponseKind,
  BuiltInRole,
  type ChangeMemberRoleRequest,
  type ChangePreferencesRequest,
  type CursorPageOfAuditEntryResponse,
  type DataExportResponse,
  ErrorCode,
  type InvitationPreviewResponse,
  type InvitationResponse,
  InvitationResponseState,
  type InviteMemberRequest,
  InviterNameResponseKind,
  Language,
  type MemberResponse,
  type MeResponse,
  type MyTenantDetailResponse,
  type MyTenantResponse,
  type MyTenantResponseStatus,
  NotificationLinkResponseKind,
  type NotificationResponse,
  NotificationType,
  Permission,
  type RenameMeRequest,
  type RoleDetailResponse,
  type RoleResponse,
} from "@repo/api-client";

// The API at the browser's door: the page's calls to /api/v1 get these answers, so a page test needs no API and no database.
// The BFF has its own tests (src/app/api/[...path]/route.test.ts).

const tenantPermissions = Object.values(Permission).filter((name) =>
  name.startsWith("tenant."),
);

// An Admin holds the Owner's permissions but the Owner's own and the management of roles (the API's BuiltInRoles).
const ownerOnly = new Set<Permission>([
  Permission.TenantOwnersManage,
  Permission.TenantRolesCreate,
  Permission.TenantRolesUpdate,
  Permission.TenantRolesDelete,
]);
const adminPermissions = tenantPermissions.filter(
  (name) => !ownerOnly.has(name),
);

const memberPermissions = [
  Permission.TenantMembersRead,
  Permission.TenantProfileRead,
];

const builtIn = (
  id: string,
  name: BuiltInRole,
  permissions: Permission[],
): RoleDetailResponse => ({
  id,
  name,
  builtIn: true,
  permissions,
  memberCount: 0,
  pendingInvitationCount: 0,
});

/** The most custom roles Northwind may have, Pro's roles.custom.max, as GetMyTenant answers it. */
export const maxCustomRoles = 20;

/** Northwind's roles, as ListRoles answers them. A request gives a role by its id. */
export const roles: Record<BuiltInRole, RoleDetailResponse> = {
  owner: builtIn(
    "0198f0a2-0000-7000-8000-0000000000f1",
    BuiltInRole.Owner,
    tenantPermissions,
  ),
  admin: builtIn(
    "0198f0a2-0000-7000-8000-0000000000f2",
    BuiltInRole.Admin,
    adminPermissions,
  ),
  member: builtIn(
    "0198f0a2-0000-7000-8000-0000000000f3",
    BuiltInRole.Member,
    memberPermissions,
  ),
};

const custom = (
  id: string,
  name: string,
  permissions: Permission[],
  memberCount = 0,
  pendingInvitationCount = 0,
): RoleDetailResponse => ({
  id,
  name,
  builtIn: false,
  permissions,
  memberCount,
  pendingInvitationCount,
});

/** Northwind's custom roles of the boards. An Admin lacks `tenant.roles.update`, so cannot give Billing lead (F51). */
export const customRoles: RoleDetailResponse[] = [
  custom(
    "0198f0a2-0000-7000-8000-0000000000c1",
    "Billing lead",
    [
      Permission.TenantProfileRead,
      Permission.TenantMembersRead,
      Permission.TenantMembersUpdate,
      Permission.TenantInvitationsRead,
      Permission.TenantInvitationsCreate,
      Permission.TenantRolesRead,
      Permission.TenantRolesUpdate,
      Permission.TenantAuditRead,
    ],
    1,
    1,
  ),
  custom("0198f0a2-0000-7000-8000-0000000000c2", "Support", [
    Permission.TenantProfileRead,
    Permission.TenantMembersRead,
    Permission.TenantInvitationsRead,
    Permission.TenantAuditRead,
  ]),
  custom("0198f0a2-0000-7000-8000-0000000000c3", "Reviewer", [
    Permission.TenantProfileRead,
    Permission.TenantMembersRead,
    Permission.TenantAuditRead,
  ]),
];

/** A role as a member, an invitation or a tenant holds it. */
const roleOf = ({ id, name, builtIn }: RoleDetailResponse): RoleResponse => ({
  id,
  name,
  builtIn,
});

/** The role of an id, from the list that ListRoles answers. */
const roleById = (id: string) =>
  [...Object.values(roles), ...customRoles].find((role) => role.id === id);

export const northwind: MyTenantResponse = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
  name: "Northwind Traders",
  slug: "northwind",
  status: "active",
  role: roleOf(roles.owner),
};

export const me = (language: Language): MeResponse => ({
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e30",
  email: "ayse@northwind.io",
  name: "Ayşe Yılmaz",
  isSystemAdmin: false,
  language,
  timeZone: "Europe/Dublin",
});

/** A tenant's plan as GetMyTenant answers it: the plan's name, the effective value of each key and the count of each limit. */
export type Plan = Pick<
  MyTenantDetailResponse,
  "planName" | "entitlements" | "usage"
>;

/** Pro, Northwind's plan: custom roles on, 20 of them, members without a limit; the counts are Northwind's custom roles. */
export const pro: Plan = {
  planName: "Pro",
  entitlements: {
    "members.max": null,
    "roles.custom": true,
    "roles.custom.max": maxCustomRoles,
  },
  usage: { "members.max": 0, "roles.custom.max": customRoles.length },
};

export async function mockApi(
  page: Page,
  language: Language = Language.En,
): Promise<void> {
  await page.route("**/api/v1/me", (route) =>
    route.fulfill({ json: me(language) }),
  );
  await mockDataExport(page);
  await page.route("**/api/v1/me/tenants*", (route) =>
    route.fulfill({
      json: { items: [northwind], page: 1, pageSize: 100, total: 1 },
    }),
  );
  await page.route(`**/api/v1/tenants/${northwind.id}`, (route) =>
    route.fulfill({
      json: {
        ...northwind,
        createdAt: "2026-01-05T09:00:00Z",
        ...pro,
        permissions: tenantPermissions,
      },
    }),
  );
  await page.route(`**/api/v1/tenants/${northwind.id}/roles*`, (route) => {
    const items = [...Object.values(roles), ...customRoles];
    return route.fulfill({
      json: { items, page: 1, pageSize: 100, total: items.length },
    });
  });
}

export const invitationCode = `${northwind.id}.e2e-secret`;

export const pendingInvitation: InvitationPreviewResponse = {
  tenantName: northwind.name,
  inviterName: {
    kind: InviterNameResponseKind.User,
    name: "Ayşe Yılmaz",
    deleted: false,
  },
  role: roleOf(roles.member),
  expiresAt: "2099-10-23T13:32:00Z",
  state: InvitationResponseState.Pending,
};

const problem = (status: number, code: ErrorCode) => ({
  status,
  json: { status, title: "Refused", code, traceId: "00-e2e" },
});

/** The accept page's calls: the preview answers the invitation or a problem, and GetMe answers a user or 404. */
export async function mockInvitation(
  page: Page,
  preview: InvitationPreviewResponse | ErrorCode,
  { knownUser = true }: { knownUser?: boolean } = {},
): Promise<void> {
  await mockApi(page);
  if (!knownUser)
    await page.route("**/api/v1/me", (route) =>
      route.fulfill(problem(404, ErrorCode.UserNotFound)),
    );
  await page.route("**/api/v1/me/invitations/preview", (route) =>
    route.fulfill(
      typeof preview === "string"
        ? problem(preview === ErrorCode.InvitationNotFound ? 404 : 403, preview)
        : { json: preview },
    ),
  );
  await page.route("**/api/v1/me/invitations/accept", (route) =>
    route.fulfill({ json: northwind }),
  );
}

const person = (
  userId: string,
  name: string,
  mailbox: string,
  role: BuiltInRole,
  joinedAt: string,
): MemberResponse => ({
  userId,
  name,
  email: `${mailbox}@northwind.io`,
  role: roleOf(roles[role]),
  deleted: false,
  joinedAt,
});

/** The members of the boards. Ayşe, the Owner, is the signed-in person of `me`; Mehmet is an Admin, İbrahim a Member. */
export const members: MemberResponse[] = [
  person(
    me(Language.En).id,
    "Ayşe Yılmaz",
    "ayşe",
    BuiltInRole.Owner,
    "2026-03-03T09:20:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000002",
    "Mehmet Kaya",
    "mehmet",
    BuiltInRole.Admin,
    "2026-03-05T10:00:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000003",
    "Siobhan Oneill",
    "siobhan",
    BuiltInRole.Admin,
    "2026-04-11T16:05:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000004",
    "Ibrahim Bulut",
    "ibrahim",
    BuiltInRole.Member,
    "2026-06-02T10:15:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000006",
    "John Byrne",
    "john",
    BuiltInRole.Member,
    "2026-03-09T08:00:00Z",
  ),
  {
    ...person(
      "0198f0a2-0000-7000-8000-000000000007",
      "Dara Quinn",
      "dara",
      BuiltInRole.Member,
      "2026-09-01T09:30:00Z",
    ),
    role: roleOf(customRoles[0] as RoleDetailResponse),
  },
];

const inDays = (days: number) =>
  new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString();

const invited = (
  id: string,
  email: string,
  days: number,
  role: RoleDetailResponse = roles.member,
  language: Language = Language.En,
): InvitationResponse => ({
  id,
  email,
  role: roleOf(role),
  invitedBy: me(Language.En).id,
  invitedByName: { name: "Ayşe Yılmaz", deleted: false },
  language,
  createdAt: inDays(days - 7),
  expiresAt: inDays(days),
  state:
    days > 0
      ? InvitationResponseState.Pending
      : InvitationResponseState.Expired,
});

// The viewer reads in English by default; Ali's email went out in Arabic and Fatma's in Turkish.
export const invitations: InvitationResponse[] = [
  invited(
    "0198f0a2-0000-7000-8000-0000000000a1",
    "ali@northwind.io",
    6,
    roles.member,
    Language.Ar,
  ),
  invited(
    "0198f0a2-0000-7000-8000-0000000000a2",
    "fatma@northwind.io",
    3,
    roles.admin,
    Language.Tr,
  ),
  invited("0198f0a2-0000-7000-8000-0000000000a3", "conor@northwind.io", 0.5),
  invited("0198f0a2-0000-7000-8000-0000000000a4", "padraig@northwind.io", -4),
  invited(
    "0198f0a2-0000-7000-8000-0000000000a6",
    "niall@northwind.io",
    5,
    customRoles[0],
  ),
];

/**
 * The members page's calls for a viewer of the role: GetMyTenant with the role's permissions, the two lists, and the commands,
 * which answer success.
 */
export async function mockMembers(
  page: Page,
  {
    role = BuiltInRole.Owner,
    language = Language.En,
    plan = pro,
    team,
  }: {
    role?: BuiltInRole;
    language?: Language;
    plan?: Plan;
    /** The team of a section of the design, in place of the boards' members, invitations and roles. */
    team?: Team;
  } = {},
): Promise<void> {
  await mockApi(page, language);
  const viewer = members.find((member) => member.role.id === roles[role].id);
  await page.route("**/api/v1/me", (route) =>
    route.fulfill({
      json: {
        ...me(language),
        id: viewer?.userId,
        name: viewer?.name,
        email: viewer?.email,
      },
    }),
  );
  await page.route(`**/api/v1/tenants/${northwind.id}`, (route) =>
    route.fulfill({
      json: {
        ...northwind,
        role: roleOf(roles[role]),
        createdAt: "2026-01-05T09:00:00Z",
        ...plan,
        permissions: roles[role].permissions,
      },
    }),
  );
  await page.route(`**/api/v1/tenants/${northwind.id}/members?*`, (route) =>
    route.fulfill({
      json: { items: members, page: 1, pageSize: 20, total: members.length },
    }),
  );
  await page.route(`**/api/v1/tenants/${northwind.id}/members/*`, (route) => {
    if (route.request().method() !== "PATCH")
      return route.fulfill({ status: 204 });
    const { roleId } = route
      .request()
      .postDataJSON() as ChangeMemberRoleRequest;
    const given = roleById(roleId);
    if (!given) return route.fulfill(problem(404, ErrorCode.RoleNotFound));
    return route.fulfill({ json: { ...members[4], role: roleOf(given) } });
  });
  await page.route(`**/api/v1/tenants/${northwind.id}/invitations?*`, (route) =>
    route.fulfill({
      json: {
        items: invitations,
        page: 1,
        pageSize: 20,
        total: invitations.length,
      },
    }),
  );
  await page.route(`**/api/v1/tenants/${northwind.id}/invitations`, (route) => {
    const { email, roleId, language } = route
      .request()
      .postDataJSON() as InviteMemberRequest;
    const given = roleById(roleId);
    if (!given) return route.fulfill(problem(404, ErrorCode.RoleNotFound));
    return route.fulfill({
      status: 201,
      json: invited(
        "0198f0a2-0000-7000-8000-0000000000a5",
        email,
        7,
        given,
        language ?? Language.En,
      ),
    });
  });
  await page.route(`**/api/v1/tenants/${northwind.id}/invitations/*`, (route) =>
    route.fulfill({ status: 204 }),
  );
  if (team) await mockTeam(page, team, { role, language, plan });
}

/** A team that a section of the design draws: the organisation's name and creation, its members, invitations and custom roles. */
export type Team = {
  organisation: string;
  createdAt: string;
  members: MemberResponse[];
  invitations: InvitationResponse[];
  customRoles: RoleDetailResponse[];
};

const teamCustom = (n: number, name: string, permissions: Permission[]) =>
  custom(`0198f0a2-0000-7000-8000-0000000002c${n}`, name, permissions);

/** The custom roles of the Members frames, in the names of a language: Support lead and Onboarding an Admin may give; Role manager and Auditor not. */
const teamRoles = (names: [string, string, string, string]) => [
  teamCustom(1, names[0], [
    Permission.TenantProfileRead,
    Permission.TenantMembersRead,
    Permission.TenantMembersUpdate,
    Permission.TenantInvitationsRead,
    Permission.TenantInvitationsCreate,
    Permission.TenantRolesRead,
    Permission.TenantAuditRead,
  ]),
  teamCustom(2, names[1], [
    Permission.TenantProfileRead,
    Permission.TenantMembersRead,
    Permission.TenantRolesRead,
    Permission.TenantRolesCreate,
    Permission.TenantRolesUpdate,
    Permission.TenantRolesDelete,
  ]),
  teamCustom(
    3,
    names[2],
    tenantPermissions.filter((name) => name !== Permission.TenantOwnersManage),
  ),
  teamCustom(4, names[3], [
    Permission.TenantProfileRead,
    Permission.TenantMembersRead,
    Permission.TenantInvitationsRead,
    Permission.TenantInvitationsCreate,
  ]),
];

/** A member of a frame; the first one is the signed-in person of `me`. */
const teammate = (
  n: number,
  name: string,
  email: string,
  role: RoleDetailResponse,
  joinedOn: string,
): MemberResponse => ({
  userId:
    n === 1
      ? me(Language.En).id
      : `0198f0a2-0000-7000-8000-0000000002${String(n).padStart(2, "0")}`,
  name,
  email,
  role: roleOf(role),
  deleted: false,
  joinedAt: `${joinedOn}T09:00:00Z`,
});

const northwindRoles = teamRoles([
  "Support lead",
  "Role manager",
  "Auditor",
  "Onboarding",
]);
const [supportLead, roleManager] = northwindRoles as [
  RoleDetailResponse,
  RoleDetailResponse,
];

/**
 * Northwind as the Members frames draw it (8.01): 11 people, then four more for the frames at and over a limit (8.43, 8.45). Grace
 * Okafor is a Member: the frames' Billing manager is step 40's.
 */
const northwindPeople = [
  teammate(
    1,
    "Ayşe Demir",
    "ayse@northwind.example",
    roles.owner,
    "2026-03-12",
  ),
  teammate(
    2,
    "Omar Haddad",
    "omar@northwind.example",
    roles.admin,
    "2026-03-14",
  ),
  teammate(
    3,
    "Lena Fischer",
    "lena@northwind.example",
    roles.member,
    "2026-04-02",
  ),
  teammate(
    4,
    "Kenji Sato",
    "kenji@northwind.example",
    supportLead,
    "2026-05-09",
  ),
  teammate(
    5,
    "Maria Santos",
    "maria@northwind.example",
    roles.member,
    "2026-06-21",
  ),
  teammate(
    6,
    "Liam O’Connor",
    "liam@northwind.example",
    roleManager,
    "2026-08-03",
  ),
  teammate(
    7,
    "Sofia Rossi",
    "sofia@northwind.example",
    roles.member,
    "2026-09-18",
  ),
  teammate(
    8,
    "Jonas Berg",
    "jonas@northwind.example",
    roles.admin,
    "2026-09-22",
  ),
  teammate(
    9,
    "Priya Nair",
    "priya@northwind.example",
    roles.member,
    "2026-10-01",
  ),
  teammate(
    10,
    "Tomás García",
    "tomas@northwind.example",
    roles.member,
    "2026-10-09",
  ),
  teammate(
    11,
    "Grace Okafor",
    "grace@northwind.example",
    roles.member,
    "2026-10-20",
  ),
  teammate(
    12,
    "Niamh Walsh",
    "niamh@northwind.example",
    roles.member,
    "2026-10-22",
  ),
  teammate(
    13,
    "Ahmed Saleh",
    "ahmed@northwind.example",
    roles.member,
    "2026-10-24",
  ),
  teammate(
    14,
    "Chloé Martin",
    "chloe@northwind.example",
    roles.member,
    "2026-10-27",
  ),
  teammate(
    15,
    "Daniel Park",
    "daniel@northwind.example",
    roles.member,
    "2026-10-29",
  ),
];

/** An invitation of a frame, sent by the member. */
const invitedBy = (
  invitation: InvitationResponse,
  by: MemberResponse,
): InvitationResponse => ({
  ...invitation,
  invitedBy: by.userId,
  invitedByName: { name: by.name, deleted: false },
});

/** The two pending invitations of the Invitations frame (8.29): David's for a week, Inès's for half a day, so it expires soon. */
const northwindInvitations = [
  invitedBy(
    invited(
      "0198f0a2-0000-7000-8000-0000000002a1",
      "david.okafor@harbour.example",
      8,
    ),
    northwindPeople[0] as MemberResponse,
  ),
  invitedBy(
    invited(
      "0198f0a2-0000-7000-8000-0000000002a2",
      "ines.moreau@northwind.example",
      0.5,
      roles.admin,
    ),
    northwindPeople[1] as MemberResponse,
  ),
];

/** Northwind as the Members frames draw it, with the first `count` people (11 in most frames). */
export const northwindTeam = (count = 11): Team => ({
  organisation: northwind.name,
  createdAt: "2026-03-12T09:00:00Z",
  members: northwindPeople.slice(0, count),
  invitations: northwindInvitations,
  customRoles: northwindRoles,
});

/** The rows of 500 members, loading more (8.05): a long name with a long email, and two Arabic names. */
export const manyMembers = [
  ...northwindPeople.slice(0, 3),
  teammate(
    16,
    "Maximiliane Alexandra von Hohenzollern-Sigmaringen",
    "maximiliane.von-hohenzollern@internationale-handelsgesellschaft.example",
    supportLead,
    "2026-05-09",
  ),
  teammate(
    17,
    "عبد الرحمن الهاشمي",
    "abdulrahman@northwind.example",
    roles.member,
    "2026-06-21",
  ),
  teammate(
    18,
    "فاطمة الزهراء القحطاني",
    "fatima@northwind.example",
    roleManager,
    "2026-08-03",
  ),
  ...northwindPeople.slice(6, 9),
];

const ufukRoles = teamRoles(["قائد الدعم", "مدير الأدوار", "مدقق", "التهيئة"]);
const ufukSupportLead = ufukRoles[0] as RoleDetailResponse;

/** The organisation of the Arabic frame (8.36): its name, ten members, the Owner first. */
export const ufukTeam: Team = {
  organisation: "شركة الأفق للتجارة",
  createdAt: "2026-03-12T09:00:00Z",
  members: [
    teammate(
      1,
      "عبد الرحمن الهاشمي",
      "abdulrahman@ufuk.example",
      roles.owner,
      "2026-03-12",
    ),
    teammate(2, "Omar Haddad", "omar@ufuk.example", roles.admin, "2026-03-14"),
    teammate(
      3,
      "فاطمة الزهراء القحطاني",
      "fatima@ufuk.example",
      roles.member,
      "2026-04-02",
    ),
    teammate(
      4,
      "Kenji Sato",
      "kenji@ufuk.example",
      ufukSupportLead,
      "2026-05-09",
    ),
    teammate(
      5,
      "يوسف بن خالد",
      "yusuf@ufuk.example",
      roles.member,
      "2026-06-21",
    ),
    teammate(
      6,
      "Lena Fischer",
      "lena@ufuk.example",
      roles.member,
      "2026-08-03",
    ),
    teammate(
      7,
      "Sofia Rossi",
      "sofia@ufuk.example",
      roles.member,
      "2026-09-18",
    ),
    teammate(8, "Jonas Berg", "jonas@ufuk.example", roles.admin, "2026-09-22"),
    teammate(9, "Priya Nair", "priya@ufuk.example", roles.member, "2026-10-01"),
    teammate(
      10,
      "Tomás García",
      "tomas@ufuk.example",
      roles.member,
      "2026-10-09",
    ),
  ],
  invitations: northwindInvitations,
  customRoles: ufukRoles,
};

/**
 * The team's answers over `mockMembers`'s: the organisation's name and creation, GetMe as the team's first person of the role,
 * the roles with their counts, the lists with the API's search, role filter and page size, and two commands that change the
 * lists as the API does. Remove takes the member out; Invite refuses a member's address (409 `member.exists`) and puts the new
 * invitation first.
 */
async function mockTeam(
  page: Page,
  team: Team,
  {
    role,
    language,
    plan,
  }: { role: BuiltInRole; language: Language; plan: Plan },
): Promise<void> {
  const viewer = team.members.find(
    (member) => member.role.id === roles[role].id,
  );
  let people = team.members;
  const sent: InvitationResponse[] = [];
  const counted = (choice: RoleDetailResponse): RoleDetailResponse => ({
    ...choice,
    memberCount: people.filter((member) => member.role.id === choice.id).length,
    pendingInvitationCount: team.invitations.filter(
      (invitation) => invitation.role.id === choice.id,
    ).length,
  });
  const tenantRoles = () =>
    [...Object.values(roles), ...team.customRoles].map(counted);
  const tenant = {
    ...northwind,
    name: team.organisation,
    role: roleOf(roles[role]),
  };
  /** A page of the list, as the API answers the request's `page` and `pageSize`. */
  const pageOf = <T>(route: Route, items: T[]) => {
    const query = new URL(route.request().url()).searchParams;
    const size = Number(query.get("pageSize") ?? 20);
    const number = Number(query.get("page") ?? 1);
    return route.fulfill({
      json: {
        items: items.slice((number - 1) * size, number * size),
        page: number,
        pageSize: size,
        total: items.length,
      },
    });
  };

  await page.route("**/api/v1/me", (route) =>
    route.fulfill({
      json: {
        ...me(language),
        id: viewer?.userId,
        name: viewer?.name,
        email: viewer?.email,
      },
    }),
  );
  await page.route("**/api/v1/me/tenants*", (route) =>
    route.fulfill({
      json: { items: [tenant], page: 1, pageSize: 100, total: 1 },
    }),
  );
  await page.route(`**/api/v1/tenants/${northwind.id}`, (route) =>
    route.fulfill({
      json: {
        ...tenant,
        createdAt: team.createdAt,
        ...plan,
        permissions: roles[role].permissions,
      },
    }),
  );
  await page.route(`**/api/v1/tenants/${northwind.id}/roles*`, (route) =>
    pageOf(route, tenantRoles()),
  );
  await page.route(`**/api/v1/tenants/${northwind.id}/members?*`, (route) => {
    const query = new URL(route.request().url()).searchParams;
    const q = query.get("q")?.toLocaleLowerCase();
    const roleId = query.get("roleId");
    return pageOf(
      route,
      people.filter(
        (member) =>
          (!q ||
            `${member.name} ${member.email}`.toLocaleLowerCase().includes(q)) &&
          (!roleId || member.role.id === roleId),
      ),
    );
  });
  await page.route(`**/api/v1/tenants/${northwind.id}/members/*`, (route) => {
    if (route.request().method() !== "DELETE") return route.fallback();
    const userId = new URL(route.request().url()).pathname.split("/").pop();
    people = people.filter((member) => member.userId !== userId);
    return route.fulfill({ status: 204 });
  });
  await page.route(`**/api/v1/tenants/${northwind.id}/invitations?*`, (route) =>
    pageOf(route, [...sent, ...team.invitations]),
  );
  await page.route(`**/api/v1/tenants/${northwind.id}/invitations`, (route) => {
    const body = route.request().postDataJSON() as InviteMemberRequest;
    if (people.some((member) => member.email === body.email))
      return route.fulfill(problem(409, ErrorCode.MemberExists));
    const given = tenantRoles().find((choice) => choice.id === body.roleId);
    if (!given || !viewer)
      return route.fulfill(problem(404, ErrorCode.RoleNotFound));
    const invitation = invitedBy(
      invited(
        "0198f0a2-0000-7000-8000-0000000002a5",
        body.email,
        7,
        given,
        body.language ?? Language.En,
      ),
      viewer,
    );
    sent.unshift(invitation);
    return route.fulfill({ status: 201, json: invitation });
  });
}

/** The web's members export (the maintainer's decision of 2026-11-07): the file under its name, or a problem of the server. */
export async function mockMembersExport(
  page: Page,
  answer: "file" | "problem",
): Promise<void> {
  await page.route(
    `**/api/exports/tenants/${northwind.id}/members*`,
    (route) =>
      answer === "file"
        ? route.fulfill({
            headers: {
              "Content-Type": "text/csv; charset=utf-8",
              "Content-Disposition":
                'attachment; filename="northwind-members-2026-11-01.csv"',
            },
            body: "Name,Email,Role,Joined\nAyşe Demir,ayse@northwind.example,Owner,2026-03-12\n",
          })
        : route.fulfill({
            status: 500,
            json: {
              status: 500,
              title: "Internal Server Error",
              traceId: "00-e2e",
            },
          }),
  );
}

/** ListMyTenants gives Northwind the status, and the viewer the role there. */
export async function mockTenantStatus(
  page: Page,
  status: MyTenantResponseStatus,
  { role = BuiltInRole.Admin }: { role?: BuiltInRole } = {},
): Promise<void> {
  await mockApi(page);
  await page.route("**/api/v1/me/tenants*", (route) =>
    route.fulfill({
      json: {
        items: [{ ...northwind, status, role: roleOf(roles[role]) }],
        page: 1,
        pageSize: 100,
        total: 1,
      },
    }),
  );
}

const auditEntry = (
  id: string,
  occurredAt: string,
  operation: AuditOperation,
  actor: string | null,
): AuditEntryResponse => ({
  id,
  occurredAt,
  operation,
  details: null,
  actor: actor
    ? {
        kind: AuditActorResponseKind.User,
        userId: me(Language.En).id,
        displayName: actor,
        deleted: false,
      }
    : {
        kind: AuditActorResponseKind.Platform,
        userId: null,
        displayName: null,
        deleted: null,
      },
  target: {
    kind: AuditTargetResponseKind.User,
    id: "0198f0a2-0000-7000-8000-000000000007",
    name: "Khalid Al-Harbi",
    deleted: false,
  },
});

/** The two pages of the tenant log: the first ends with a cursor, the second with none. */
export const auditPages: Record<string, CursorPageOfAuditEntryResponse> = {
  "": {
    items: [
      auditEntry(
        "0198f0a2-0000-7000-8000-0000000000e1",
        "2026-10-16T08:40:00Z",
        AuditOperation.MemberJoined,
        "Ayşe Yılmaz",
      ),
      auditEntry(
        "0198f0a2-0000-7000-8000-0000000000e2",
        "2026-10-15T16:20:00Z",
        AuditOperation.TenantReactivated,
        null,
      ),
    ],
    nextCursor: "page-2",
  },
  "page-2": {
    items: [
      auditEntry(
        "0198f0a2-0000-7000-8000-0000000000e3",
        "2026-01-05T09:00:00Z",
        AuditOperation.TenantCreated,
        "Ayşe Yılmaz",
      ),
    ],
    nextCursor: null,
  },
};

/** ListAuditEntries answers the page of the request's cursor. */
export async function mockAuditLog(
  page: Page,
  { role = BuiltInRole.Owner }: { role?: BuiltInRole } = {},
): Promise<void> {
  await mockMembers(page, { role });
  await page.route(`**/api/v1/tenants/${northwind.id}/audit-log*`, (route) =>
    route.fulfill({
      json: auditPages[
        new URL(route.request().url()).searchParams.get("cursor") ?? ""
      ],
    }),
  );
}

/** ChangePreferences answers the body back, as the API does, and records it. */
export async function mockPreferences(
  page: Page,
  language: Language,
  bodies: ChangePreferencesRequest[] = [],
): Promise<void> {
  await mockApi(page, language);
  await page.route("**/api/v1/me/preferences", (route) => {
    const body = route.request().postDataJSON() as ChangePreferencesRequest;
    bodies.push(body);
    return route.fulfill({ json: body });
  });
}

/** RenameMe saves the names, and GetMe answers them from then on, as the API does. It records each body. */
export async function mockRename(
  page: Page,
  language: Language,
  bodies: RenameMeRequest[] = [],
): Promise<void> {
  await mockApi(page, language);
  let person = me(language);
  await page.route("**/api/v1/me", (route) => {
    const body = route.request().postDataJSON() as RenameMeRequest | null;
    if (body) {
      bodies.push(body);
      person = { ...person, ...body };
    }
    return route.fulfill({ json: person });
  });
}

export const roleChanged: NotificationResponse = {
  id: "0198f0a2-0000-7000-8000-0000000000a1",
  type: NotificationType.MemberRoleChanged,
  title: "Your role in Northwind Traders changed",
  body: "Mehmet Kaya changed your role from Member to Admin.",
  area: "Your access",
  actorName: "Mehmet Kaya",
  tenantName: "Northwind Traders",
  link: {
    kind: NotificationLinkResponseKind.TenantSettings,
    tenantId: northwind.id,
  },
  createdAt: "2026-10-16T13:29:00Z",
  readAt: null,
  unread: true,
};

/** The items a list read answers: with the filter unread, only the unread ones, as the API does. */
export const listed = (route: Route, items: NotificationResponse[]) =>
  new URL(route.request().url()).searchParams.get("unread") === "true"
    ? items.filter((item) => item.unread)
    : items;

/** What the notification answers recorded: the notifications opened and the bells marked read. */
export type NotificationCalls = {
  opened: string[];
  markedAll: number;
};

/**
 * Northwind's bell and its live stream: one notification and one unread, or the list and the count a shot names. The count answers `unread.count`; the stream holds its answer until
 * the test calls `signal`, then sends one `notifications` event for Northwind's bell and ends, with a long retry so the browser
 * does not open it again during the test.
 */
export async function mockNotifications(
  page: Page,
  list: { items?: NotificationResponse[]; unread?: number } = {},
) {
  const items = list.items ?? [roleChanged];
  const unread = { count: list.unread ?? 1 };
  const calls: NotificationCalls = { opened: [], markedAll: 0 };
  let signal = () => {};
  const sent = new Promise<void>((resolve) => {
    signal = resolve;
  });
  await page.route(
    `**/api/v1/tenants/${northwind.id}/notifications/unread-count`,
    (route) => route.fulfill({ json: { unread: unread.count } }),
  );
  // The Unread tab reads with the filter unread (F57).
  const answerList = (route: Route) =>
    route.fulfill({ json: { items: listed(route, items), nextCursor: null } });
  await page.route(
    `**/api/v1/tenants/${northwind.id}/notifications?*`,
    answerList,
  );
  await page.route(
    `**/api/v1/tenants/${northwind.id}/notifications`,
    answerList,
  );
  await page.route(
    `**/api/v1/tenants/${northwind.id}/notifications/read`,
    (route) => {
      calls.markedAll += 1;
      unread.count = 0;
      return route.fulfill({ status: 204 });
    },
  );
  await page.route("**/api/v1/me/notifications/*/read", (route) => {
    calls.opened.push(route.request().url());
    return route.fulfill({ status: 204 });
  });
  await page.route("**/api/v1/me/notifications/stream", async (route) => {
    await sent;
    await route.fulfill({
      headers: { "Content-Type": "text/event-stream" },
      body: `retry: 600000\nevent: notifications\ndata: {"scope":"tenant","tenantId":"${northwind.id}"}\n\n`,
    });
  });
  return {
    calls,
    /** One new notification: the count goes up and the stream says so. */
    arrive: () => {
      unread.count += 1;
      signal();
    },
  };
}

/** The person's latest data export for Your data (F62): none by default, GetMyDataExport's 404. */
export async function mockDataExport(
  page: Page,
  latest?: DataExportResponse,
): Promise<void> {
  await page.route("**/api/v1/me/data-exports/latest", (route) =>
    latest
      ? route.fulfill({ json: latest })
      : route.fulfill({
          status: 404,
          json: {
            code: ErrorCode.DataExportNotFound,
            traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
          },
        }),
  );
}
