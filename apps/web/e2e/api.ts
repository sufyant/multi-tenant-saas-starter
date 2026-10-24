import type { Page } from "@playwright/test";
import {
  AuditActorResponseKind,
  type AuditEntryResponse,
  AuditOperation,
  AuditTargetResponseKind,
  type ChangeMemberRoleRequest,
  type ChangePreferencesRequest,
  type CursorPageOfAuditEntryResponse,
  ErrorCode,
  type InvitationPreviewResponse,
  type InvitationResponse,
  InvitationResponseState,
  type InviteMemberRequest,
  InviterNameResponseKind,
  Language,
  type MemberResponse,
  type MeResponse,
  type MyTenantResponse,
  type MyTenantResponseStatus,
  Permission,
  type RoleDetailResponse,
  type RoleResponse,
} from "@repo/api-client";
import { BuiltInRole } from "../src/shared/roles";

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
): RoleDetailResponse => ({ id, name, builtIn: true, permissions });

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
): RoleDetailResponse => ({ id, name, builtIn: false, permissions });

/** Northwind's custom roles of the boards. An Admin lacks `tenant.roles.update`, so cannot give Billing lead (F51). */
export const customRoles: RoleDetailResponse[] = [
  custom("0198f0a2-0000-7000-8000-0000000000c1", "Billing lead", [
    Permission.TenantProfileRead,
    Permission.TenantMembersRead,
    Permission.TenantMembersUpdate,
    Permission.TenantInvitationsRead,
    Permission.TenantInvitationsCreate,
    Permission.TenantRolesRead,
    Permission.TenantRolesUpdate,
    Permission.TenantAuditRead,
  ]),
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
  firstName: "Ayşe",
  lastName: "Yılmaz",
  isSystemAdmin: false,
  language,
  timeZone: "Europe/Dublin",
});

export async function mockApi(
  page: Page,
  language: Language = Language.En,
): Promise<void> {
  await page.route("**/api/v1/me", (route) =>
    route.fulfill({ json: me(language) }),
  );
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
    firstName: "Ayşe",
    lastName: "Yılmaz",
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
  firstName: string,
  lastName: string,
  role: BuiltInRole,
  joinedAt: string,
): MemberResponse => ({
  userId,
  firstName,
  lastName,
  email: `${firstName.toLocaleLowerCase("en")}@northwind.io`,
  role: roleOf(roles[role]),
  deleted: false,
  joinedAt,
});

/** The members of the boards. Ayşe, the Owner, is the signed-in person of `me`; Mehmet is an Admin, İbrahim a Member. */
export const members: MemberResponse[] = [
  person(
    me(Language.En).id,
    "Ayşe",
    "Yılmaz",
    BuiltInRole.Owner,
    "2026-03-03T09:20:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000002",
    "Mehmet",
    "Kaya",
    BuiltInRole.Admin,
    "2026-03-05T10:00:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000003",
    "Siobhan",
    "Oneill",
    BuiltInRole.Admin,
    "2026-04-11T16:05:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000004",
    "Ibrahim",
    "Bulut",
    BuiltInRole.Member,
    "2026-06-02T10:15:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000006",
    "John",
    "Byrne",
    BuiltInRole.Member,
    "2026-03-09T08:00:00Z",
  ),
  {
    ...person(
      "0198f0a2-0000-7000-8000-000000000007",
      "Dara",
      "Quinn",
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
  invitedByName: { firstName: "Ayşe", lastName: "Yılmaz", deleted: false },
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
  }: { role?: BuiltInRole; language?: Language } = {},
): Promise<void> {
  await mockApi(page, language);
  const viewer = members.find((member) => member.role.id === roles[role].id);
  await page.route("**/api/v1/me", (route) =>
    route.fulfill({
      json: {
        ...me(language),
        id: viewer?.userId,
        firstName: viewer?.firstName,
        lastName: viewer?.lastName,
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
