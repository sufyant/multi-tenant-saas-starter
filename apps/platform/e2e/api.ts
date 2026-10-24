import type { Page } from "@playwright/test";
import {
  AuditActorResponseKind,
  type AuditEntryResponse,
  AuditOperation,
  AuditTargetResponseKind,
  BuiltInRole,
  type CreateTenantRequest,
  type CursorPageOfAuditEntryResponse,
  ErrorCode,
  InvitationResponseState,
  Language,
  type MakeOwnerRequest,
  type MeResponse,
  type RenameMeRequest,
  type RoleResponse,
  type TenantDetailResponse,
  type TenantListItemResponse,
  TenantResponseStatus,
  TenantSuspendedDetailsResponseOperation,
  type UserDetailResponse,
  type UserMembershipResponse,
  UserMembershipResponseTenantStatus,
  type UserResponse,
  UserResponseStatus,
} from "@repo/api-client";

// The API at the browser's door: the page's calls to /api/v1 get these answers, so a page test needs no API and no database.
// The BFF has its own tests (src/app/api/[...path]/route.test.ts).
export const me = (language: Language): MeResponse => ({
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e30",
  email: "admin@starter.example",
  firstName: "Sam",
  lastName: "Kelly",
  isSystemAdmin: true,
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
}

/** RenameMe saves the names, and GetMe answers them from then on, as the API does. It records each body. */
export async function mockRename(
  page: Page,
  language: Language,
  bodies: RenameMeRequest[] = [],
): Promise<void> {
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

const day = 24 * 60 * 60 * 1000;
const inDays = (days: number) =>
  new Date(Date.now() + days * day).toISOString();

const problem = (status: number, code: ErrorCode) => ({
  status,
  json: { status, title: "Refused", code, traceId: "00-e2e" },
});

const tenant = (
  id: string,
  name: string,
  slug: string,
  status: TenantResponseStatus,
  fields: Partial<TenantDetailResponse> = {},
): TenantDetailResponse => ({
  id,
  name,
  slug,
  status,
  createdAt: inDays(-90),
  suspensionReason: null,
  closedAt: null,
  purgedAt: null,
  reopenableUntil: null,
  purgeWarningAt: null,
  memberCount: 0,
  ownerCount: 0,
  owners: [],
  ownerInvitation: null,
  ...fields,
});

/**
 * The organisations of the boards, one of each state a page shows: Northwind with its Owner, Atlas waiting for its Owner,
 * Shannon suspended, Kestrel closed with its warning date ahead, and Liffey deleted. The dates are relative to now, as the API's.
 */
export const organisations = {
  northwind: tenant(
    "0198f0a2-0000-7000-8000-00000000a001",
    "Northwind Traders",
    "northwind",
    TenantResponseStatus.Active,
    {
      memberCount: 6,
      ownerCount: 1,
      owners: [
        {
          userId: "0198f0a2-0000-7000-8000-00000000b003",
          firstName: "Ayşe",
          lastName: "Yılmaz",
          email: "ayse@northwind.io",
        },
      ],
    },
  ),
  atlas: tenant(
    "0198f0a2-0000-7000-8000-00000000a002",
    "Atlas Logistics",
    "atlas",
    TenantResponseStatus.Active,
    {
      memberCount: 1,
      ownerInvitation: {
        email: "owner@atlas.example",
        expiresAt: inDays(5),
        state: InvitationResponseState.Pending,
        language: Language.En,
      },
    },
  ),
  shannon: tenant(
    "0198f0a2-0000-7000-8000-00000000a003",
    "Shannon Studio",
    "shannon",
    TenantResponseStatus.Suspended,
    { memberCount: 4, ownerCount: 1, suspensionReason: "Unpaid invoice" },
  ),
  kestrel: tenant(
    "0198f0a2-0000-7000-8000-00000000a004",
    "Kestrel Labs",
    "kestrel",
    TenantResponseStatus.Closed,
    {
      memberCount: 3,
      ownerCount: 1,
      closedAt: inDays(-10),
      reopenableUntil: inDays(20),
      purgeWarningAt: inDays(13),
    },
  ),
  liffey: tenant(
    "0198f0a2-0000-7000-8000-00000000a005",
    "Liffey Design",
    "liffey",
    TenantResponseStatus.Purged,
    { closedAt: inDays(-40), purgedAt: inDays(-10) },
  ),
};

/** The organisation that Create organisation makes: GetTenant answers it once the 201 named it. */
export const created = tenant(
  "0198f0a2-0000-7000-8000-00000000a006",
  "Corrib Analytics",
  "corrib",
  TenantResponseStatus.Active,
  {
    ownerInvitation: {
      email: "owner@corrib.example",
      expiresAt: inDays(7),
      state: InvitationResponseState.Pending,
      language: Language.En,
    },
  },
);

const listItem = ({
  owners: _,
  purgeWarningAt: __,
  ...item
}: TenantDetailResponse): TenantListItemResponse => item;

const person = (
  id: string,
  firstName: string | null,
  lastName: string | null,
  email: string | null,
  fields: Partial<UserResponse> = {},
): UserResponse => ({
  id,
  email,
  firstName,
  lastName,
  status: UserResponseStatus.Active,
  isSystemAdmin: false,
  isRootSystemAdmin: false,
  systemAdminGrantedAt: null,
  membershipCount: 0,
  createdAt: "2026-01-05T09:00:00Z",
  ...fields,
});

/**
 * The users of the boards: the root admin, whom the configuration names and who has no names yet; Sam, the signed-in admin
 * of `me`; Ayşe, the Owner of Northwind and a member of Atlas; John, who becomes a system admin; Mehmet, suspended; and a
 * deleted user, who has no name and no email.
 */
export const users = {
  root: person(
    "0198f0a2-0000-7000-8000-00000000b001",
    null,
    null,
    "root@starter.example",
    {
      isSystemAdmin: true,
      isRootSystemAdmin: true,
      systemAdminGrantedAt: "2026-01-02T08:00:00Z",
    },
  ),
  sam: person(me(Language.En).id, "Sam", "Kelly", me(Language.En).email, {
    isSystemAdmin: true,
    systemAdminGrantedAt: "2026-02-10T09:30:00Z",
  }),
  ayse: person(
    "0198f0a2-0000-7000-8000-00000000b003",
    "Ayşe",
    "Yılmaz",
    "ayse@northwind.io",
    { membershipCount: 2, createdAt: "2026-03-03T09:20:00Z" },
  ),
  john: person(
    "0198f0a2-0000-7000-8000-00000000b004",
    "John",
    "Byrne",
    "john@kestrel.example",
    { membershipCount: 1, createdAt: "2026-03-09T08:00:00Z" },
  ),
  mehmet: person(
    "0198f0a2-0000-7000-8000-00000000b005",
    "Mehmet",
    "Kaya",
    "mehmet@shannon.example",
    {
      status: UserResponseStatus.Suspended,
      membershipCount: 1,
      createdAt: "2026-04-11T16:05:00Z",
    },
  ),
  deleted: person("0198f0a2-0000-7000-8000-00000000b006", null, null, null, {
    status: UserResponseStatus.Deleted,
    createdAt: "2026-05-20T12:00:00Z",
  }),
};

const role = (name: BuiltInRole): RoleResponse => ({
  id: `0198f0a2-0000-7000-8000-0000000000f${name === BuiltInRole.Owner ? 1 : 3}`,
  name,
  builtIn: true,
});

const membership = (
  organisation: TenantDetailResponse,
  name: BuiltInRole,
  joinedAt: string,
): UserMembershipResponse => ({
  tenantId: organisation.id,
  tenantName: organisation.name,
  tenantSlug: organisation.slug,
  tenantStatus: UserMembershipResponseTenantStatus.Active,
  role: role(name),
  joinedAt,
  lastOwner: name === BuiltInRole.Owner,
});

/** Ayşe's page: the Owner of Northwind, so Make Owner is off there, and a Member of Atlas, where it is on. */
export const ayseDetail: UserDetailResponse = {
  ...users.ayse,
  suspendedAt: null,
  suspendedBy: null,
  memberships: [
    membership(
      organisations.northwind,
      BuiltInRole.Owner,
      "2026-03-03T09:20:00Z",
    ),
    membership(organisations.atlas, BuiltInRole.Member, "2026-06-02T10:15:00Z"),
  ],
};

const entry = (
  id: string,
  occurredAt: string,
  operation: AuditOperation,
  target: { kind: AuditTargetResponseKind; id: string; name: string | null },
  details: AuditEntryResponse["details"] = null,
): AuditEntryResponse => ({
  id,
  occurredAt,
  operation,
  details,
  actor: {
    kind: AuditActorResponseKind.User,
    userId: users.sam.id,
    displayName: "Sam Kelly",
    deleted: false,
  },
  target: {
    ...target,
    deleted: target.kind === AuditTargetResponseKind.User ? false : null,
  },
});

const ofTenant = ({ id, name }: TenantDetailResponse) => ({
  kind: AuditTargetResponseKind.Tenant,
  id,
  name,
});

/** The platform's log, newest first: each entry names the tenant it is about, so an organisation's tab holds its own. */
export const platformLog: (AuditEntryResponse & { tenantId: string | null })[] =
  [
    {
      ...entry(
        "0198f0a2-0000-7000-8000-00000000e001",
        "2026-10-16T08:40:00Z",
        AuditOperation.TenantCreated,
        ofTenant(organisations.atlas),
      ),
      tenantId: organisations.atlas.id,
    },
    {
      ...entry(
        "0198f0a2-0000-7000-8000-00000000e002",
        "2026-10-15T16:20:00Z",
        AuditOperation.TenantSuspended,
        ofTenant(organisations.shannon),
        {
          operation: TenantSuspendedDetailsResponseOperation.TenantSuspended,
          reason: "Unpaid invoice",
        },
      ),
      tenantId: organisations.shannon.id,
    },
    {
      ...entry(
        "0198f0a2-0000-7000-8000-00000000e003",
        "2026-10-14T11:05:00Z",
        AuditOperation.SystemAdminGranted,
        {
          kind: AuditTargetResponseKind.User,
          id: users.sam.id,
          name: "Sam Kelly",
        },
      ),
      tenantId: null,
    },
    {
      ...entry(
        "0198f0a2-0000-7000-8000-00000000e004",
        "2026-10-02T09:00:00Z",
        AuditOperation.TenantClosed,
        ofTenant(organisations.kestrel),
      ),
      tenantId: organisations.kestrel.id,
    },
  ];

/** What the platform's pages read: GetMe, the organisations, the users and the platform's log. */
export async function mockPlatform(
  page: Page,
  language: Language = Language.En,
): Promise<void> {
  await mockApi(page, language);
  const details = [...Object.values(organisations), created];
  await page.route("**/api/v1/platform/tenants*", (route) => {
    const status = new URL(route.request().url()).searchParams.get("status");
    const items = Object.values(organisations)
      .filter((organisation) => !status || organisation.status === status)
      .map(listItem);
    return route.fulfill({
      json: { items, page: 1, pageSize: 20, total: items.length },
    });
  });
  await page.route("**/api/v1/platform/tenants/*", (route) => {
    const id = new URL(route.request().url()).pathname.split("/").at(-1);
    const found = details.find((organisation) => organisation.id === id);
    return route.fulfill(
      found ? { json: found } : problem(404, ErrorCode.TenantNotFound),
    );
  });
  await page.route("**/api/v1/platform/users*", (route) => {
    const params = new URL(route.request().url()).searchParams;
    const q = params.get("q")?.toLocaleLowerCase("en");
    const items = Object.values(users).filter(
      (user) =>
        (params.get("isSystemAdmin") !== "true" || user.isSystemAdmin) &&
        (!q ||
          [user.firstName, user.lastName, user.email].some((text) =>
            text?.toLocaleLowerCase("en").includes(q),
          )),
    );
    return route.fulfill({
      json: { items, page: 1, pageSize: 20, total: items.length },
    });
  });
  await page.route(`**/api/v1/platform/users/${users.ayse.id}`, (route) =>
    route.fulfill({ json: ayseDetail }),
  );
  await page.route("**/api/v1/platform/audit-log*", (route) => {
    const tenantId = new URL(route.request().url()).searchParams.get(
      "tenantId",
    );
    const answer: CursorPageOfAuditEntryResponse = {
      items: platformLog
        .filter((item) => !tenantId || item.tenantId === tenantId)
        .map(({ tenantId: _, ...item }) => item),
      nextCursor: null,
    };
    return route.fulfill({ json: answer });
  });
}

/** CreateTenant answers 201 with the new organisation's address in `Location`, as the API does. It records each body. */
export async function mockCreate(
  page: Page,
  bodies: CreateTenantRequest[] = [],
): Promise<void> {
  await page.route("**/api/v1/platform/tenants", (route) => {
    if (route.request().method() !== "POST") return route.fallback();
    bodies.push(route.request().postDataJSON() as CreateTenantRequest);
    const { owners: _, purgeWarningAt: __, ...answer } = created;
    return route.fulfill({
      status: 201,
      headers: { Location: `/v1/platform/tenants/${created.id}` },
      json: { ...listItem(created), ...answer },
    });
  });
}

/** CloseTenant closes Northwind: from then on GetTenant answers it closed, with the API's reopen date. */
export async function mockClose(page: Page): Promise<void> {
  const { northwind } = organisations;
  let closed = false;
  await page.route(
    `**/api/v1/platform/tenants/${northwind.id}/close`,
    (route) => {
      closed = true;
      return route.fulfill({
        json: { ...northwind, status: TenantResponseStatus.Closed },
      });
    },
  );
  await page.route(`**/api/v1/platform/tenants/${northwind.id}`, (route) =>
    route.fulfill({
      json: closed
        ? {
            ...northwind,
            status: TenantResponseStatus.Closed,
            closedAt: new Date().toISOString(),
            reopenableUntil: inDays(30),
          }
        : northwind,
    }),
  );
}

/** MakeOwner answers the membership with the Owner role, and records each body. */
export async function mockMakeOwner(
  page: Page,
  bodies: MakeOwnerRequest[] = [],
): Promise<void> {
  const atlas = ayseDetail.memberships[1] as UserMembershipResponse;
  await page.route(
    `**/api/v1/platform/tenants/${atlas.tenantId}/owners`,
    (route) => {
      bodies.push(route.request().postDataJSON() as MakeOwnerRequest);
      return route.fulfill({
        json: { ...atlas, role: role(BuiltInRole.Owner), lastOwner: false },
      });
    },
  );
}

/**
 * GrantSystemAdmin for John: the first `refusals` requests answer 403 `system_admin.reverification_required` (the API's A6a),
 * the next one John as a system admin. It records the Idempotency-Key of each request.
 */
export async function mockGrant(
  page: Page,
  { refusals = 0, keys = [] }: { refusals?: number; keys?: string[] } = {},
): Promise<void> {
  await page.route(
    `**/api/v1/platform/users/${users.john.id}/system-admin`,
    (route) => {
      keys.push(route.request().headers()["idempotency-key"] ?? "");
      if (keys.length <= refusals)
        return route.fulfill(
          problem(403, ErrorCode.SystemAdminReverificationRequired),
        );
      return route.fulfill({
        json: {
          ...users.john,
          isSystemAdmin: true,
          systemAdminGrantedAt: new Date().toISOString(),
        },
      });
    },
  );
}
