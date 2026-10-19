import type { Page } from "@playwright/test";
import {
  ErrorCode,
  type InvitationPreviewResponse,
  type InvitationResponse,
  InvitationResponseState,
  InviterNameResponseKind,
  Language,
  type MemberResponse,
  MemberRole,
  type MeResponse,
  type MyTenantResponse,
  Permission,
} from "@repo/api-client";

// The API at the browser's door: the page's calls to /api/v1 get these answers, so a page test needs no API and no database.
// The BFF has its own tests (src/app/api/[...path]/route.test.ts).
export const northwind: MyTenantResponse = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
  name: "Northwind Traders",
  slug: "northwind",
  status: "active",
  role: { builtIn: MemberRole.Owner, name: MemberRole.Owner },
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
        permissions: Object.values(Permission).filter((name) =>
          name.startsWith("tenant."),
        ),
      },
    }),
  );
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
  role: { builtIn: MemberRole.Member, name: MemberRole.Member },
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
  role: MemberRole,
  joinedAt: string,
): MemberResponse => ({
  userId,
  firstName,
  lastName,
  email: `${firstName.toLocaleLowerCase("en")}@northwind.io`,
  role: { builtIn: role, name: role },
  deleted: false,
  joinedAt,
});

/** The members of the boards. Ayşe, the Owner, is the signed-in person of `me`; Mehmet is an Admin, İbrahim a Member. */
export const members: MemberResponse[] = [
  person(
    me(Language.En).id,
    "Ayşe",
    "Yılmaz",
    MemberRole.Owner,
    "2026-03-03T09:20:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000002",
    "Mehmet",
    "Kaya",
    MemberRole.Admin,
    "2026-03-05T10:00:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000003",
    "Siobhan",
    "Oneill",
    MemberRole.Admin,
    "2026-04-11T16:05:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000004",
    "Ibrahim",
    "Bulut",
    MemberRole.Member,
    "2026-06-02T10:15:00Z",
  ),
  person(
    "0198f0a2-0000-7000-8000-000000000006",
    "John",
    "Byrne",
    MemberRole.Member,
    "2026-03-09T08:00:00Z",
  ),
];

const inDays = (days: number) =>
  new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString();

const invited = (
  id: string,
  email: string,
  days: number,
  role: MemberRole = MemberRole.Member,
): InvitationResponse => ({
  id,
  email,
  role: { builtIn: role, name: role },
  invitedBy: me(Language.En).id,
  invitedByName: { firstName: "Ayşe", lastName: "Yılmaz", deleted: false },
  createdAt: inDays(days - 7),
  expiresAt: inDays(days),
  state:
    days > 0
      ? InvitationResponseState.Pending
      : InvitationResponseState.Expired,
});

export const invitations: InvitationResponse[] = [
  invited("0198f0a2-0000-7000-8000-0000000000a1", "ali@northwind.io", 6),
  invited(
    "0198f0a2-0000-7000-8000-0000000000a2",
    "fatma@northwind.io",
    3,
    MemberRole.Admin,
  ),
  invited("0198f0a2-0000-7000-8000-0000000000a3", "conor@northwind.io", 0.5),
  invited("0198f0a2-0000-7000-8000-0000000000a4", "padraig@northwind.io", -4),
];

const memberPermissions = [
  Permission.TenantMembersRead,
  Permission.TenantProfileRead,
];

/**
 * The members page's calls for a viewer of the role: GetMyTenant with the role's permissions, the two lists, and the commands,
 * which answer success.
 */
export async function mockMembers(
  page: Page,
  {
    role = MemberRole.Owner,
    language = Language.En,
  }: { role?: MemberRole; language?: Language } = {},
): Promise<void> {
  await mockApi(page, language);
  const viewer = members.find((member) => member.role.builtIn === role);
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
        role: { builtIn: role, name: role },
        createdAt: "2026-01-05T09:00:00Z",
        permissions:
          role === MemberRole.Member
            ? memberPermissions
            : Object.values(Permission).filter((name) =>
                name.startsWith("tenant."),
              ),
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
    const { role: given } = route.request().postDataJSON() as {
      role: MemberRole;
    };
    return route.fulfill({
      json: { ...members[4], role: { builtIn: given, name: given } },
    });
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
  await page.route(`**/api/v1/tenants/${northwind.id}/invitations`, (route) =>
    route.fulfill({
      status: 201,
      json: invited(
        "0198f0a2-0000-7000-8000-0000000000a5",
        route.request().postDataJSON().email,
        7,
      ),
    }),
  );
  await page.route(`**/api/v1/tenants/${northwind.id}/invitations/*`, (route) =>
    route.fulfill({ status: 204 }),
  );
}
