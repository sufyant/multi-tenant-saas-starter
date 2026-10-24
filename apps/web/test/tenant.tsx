import {
  type MemberResponse,
  type MeResponse,
  type MyTenantResponse,
  Permission,
  type RoleDetailResponse,
  type RoleResponse,
} from "@repo/api-client";
import { Toaster } from "@repo/ui/components/toast";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import type { ReactNode } from "react";
import { BuiltInRole } from "@/shared/roles";
import { TenantProvider } from "@/shared/tenant-context";
import { renderWithProviders } from "./render";

// The sample organisation and its people (the design system's sample data), and the answers a tenant screen reads first:
// ListMyTenants, GetMyTenant, GetMe and ListRoles. Each role gets the permissions the API gives it.

/** Northwind's built-in roles, each with its id, as the API answers them. */
export const roleOf: Record<BuiltInRole, RoleResponse> = {
  owner: {
    id: "0198f0a2-0000-7000-8000-0000000000f1",
    name: BuiltInRole.Owner,
    builtIn: true,
  },
  admin: {
    id: "0198f0a2-0000-7000-8000-0000000000f2",
    name: BuiltInRole.Admin,
    builtIn: true,
  },
  member: {
    id: "0198f0a2-0000-7000-8000-0000000000f3",
    name: BuiltInRole.Member,
    builtIn: true,
  },
};

export const northwind: MyTenantResponse = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
  name: "Northwind Traders",
  slug: "northwind",
  status: "active",
  role: roleOf.owner,
};

const member = (
  userId: string,
  firstName: string,
  lastName: string,
  email: string,
  role: BuiltInRole,
  joinedAt: string,
): MemberResponse => ({
  userId,
  firstName,
  lastName,
  email,
  role: roleOf[role],
  deleted: false,
  joinedAt,
});

export const ayse = member(
  "0198f0a2-0000-7000-8000-000000000001",
  "Ayşe",
  "Yılmaz",
  "ayse@northwind.io",
  BuiltInRole.Owner,
  "2026-03-03T09:20:00Z",
);
export const mehmet = member(
  "0198f0a2-0000-7000-8000-000000000002",
  "Mehmet",
  "Kaya",
  "mehmet@northwind.io",
  BuiltInRole.Admin,
  "2026-03-05T10:00:00Z",
);
export const ibrahim = member(
  "0198f0a2-0000-7000-8000-000000000004",
  "İbrahim",
  "Bulut",
  "ibrahim@northwind.io",
  BuiltInRole.Member,
  "2026-06-02T10:15:00Z",
);
export const john = member(
  "0198f0a2-0000-7000-8000-000000000006",
  "John",
  "Byrne",
  "john@northwind.io",
  BuiltInRole.Member,
  "2026-03-09T08:00:00Z",
);
export const people = [ayse, mehmet, ibrahim, john];

/** The signed-in person of each role: the Owner Ayşe, the Admin Mehmet, the Member İbrahim. */
export const viewerOf: Record<BuiltInRole, MemberResponse> = {
  owner: ayse,
  admin: mehmet,
  member: ibrahim,
};

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

/** The permissions of a built-in role, as GetMyTenant answers them. */
export const permissionsOf: Record<BuiltInRole, Permission[]> = {
  owner: tenantPermissions,
  admin: tenantPermissions.filter((name) => !ownerOnly.has(name)),
  member: [Permission.TenantMembersRead, Permission.TenantProfileRead],
};

/** A custom role of Northwind: its own name and permissions. */
export const customRole = (
  id: string,
  name: string,
  permissions: Permission[],
): RoleDetailResponse => ({
  id,
  name,
  builtIn: false,
  permissions,
  memberCount: 0,
  pendingInvitationCount: 0,
});

/** Mehmet's custom role in the task: it changes roles but neither invites nor reads Activity. */
export const supportLead = customRole(
  "0198f0a2-0000-7000-8000-0000000000c1",
  "Support lead",
  [
    Permission.TenantProfileRead,
    Permission.TenantMembersRead,
    Permission.TenantMembersUpdate,
    Permission.TenantRolesRead,
  ],
);

/** A custom role that holds Activity, which Support lead does not. */
export const billingLead = customRole(
  "0198f0a2-0000-7000-8000-0000000000c2",
  "Billing lead",
  [
    Permission.TenantProfileRead,
    Permission.TenantMembersRead,
    Permission.TenantAuditRead,
  ],
);

const meOfPerson = (person: MemberResponse): MeResponse => ({
  id: person.userId,
  email: person.email,
  firstName: person.firstName,
  lastName: person.lastName,
  isSystemAdmin: false,
  language: "en",
  timeZone: "Europe/Dublin",
});

export const meOf = (role: BuiltInRole): MeResponse =>
  meOfPerson(viewerOf[role]);

/** ListMyTenants, GetMyTenant, GetMe and ListRoles for a viewer of the role. */
export const tenantAnswers = (
  role: BuiltInRole,
  permissions: Permission[] = permissionsOf[role],
  custom: RoleDetailResponse[] = [],
) => viewerAnswers(roleOf[role], permissions, viewerOf[role], custom);

/** ListMyTenants, GetMyTenant, GetMe and ListRoles for a viewer of any role, a custom one too. */
export const viewerAnswers = (
  role: RoleResponse,
  permissions: Permission[],
  person: MemberResponse,
  custom: RoleDetailResponse[] = [],
) => [
  http.get("*/v1/me/tenants", () =>
    HttpResponse.json({ items: [northwind], page: 1, pageSize: 100, total: 1 }),
  ),
  http.get(`*/v1/tenants/${northwind.id}`, () =>
    HttpResponse.json({
      ...northwind,
      role,
      createdAt: "2026-01-05T09:00:00Z",
      permissions,
    }),
  ),
  http.get("*/v1/me", () => HttpResponse.json(meOfPerson(person))),
  rolesAnswer(custom),
];

/** ListRoles: Northwind's built-in roles with their permissions, and the custom roles given. */
export function rolesAnswer(custom: RoleDetailResponse[] = []) {
  const items = [
    ...Object.values(BuiltInRole).map((name) => ({
      ...roleOf[name],
      permissions: permissionsOf[name],
    })),
    ...custom,
  ];
  return http.get(`*/v1/tenants/${northwind.id}/roles`, () =>
    HttpResponse.json({ items, page: 1, pageSize: 100, total: items.length }),
  );
}

/** A Problem Details answer of the API (N11g). */
export const problem = (status: number, code: string) =>
  HttpResponse.json(
    {
      status,
      title: "Refused",
      code,
      traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    },
    { status },
  );

/** Renders a screen of the organisation at an address, with the toasts, as the tenant layout and the root layout do. */
export function renderInTenant(
  ui: ReactNode,
  {
    url = "/northwind/settings/members",
    locale = "en",
  }: { url?: string; locale?: "en" | "tr" | "ar" } = {},
) {
  window.history.replaceState(null, "", url);
  return renderWithProviders(
    <>
      <TenantProvider slug={northwind.slug}>{ui}</TenantProvider>
      <Toaster dir="ltr" closeLabel="Close" regionLabel="Notifications" />
    </>,
    locale,
  );
}

/** ListMembers that answers the people and records each request's query. */
export function membersAnswer(
  items: MemberResponse[] = people,
  requests: URLSearchParams[] = [],
) {
  return http.get(`*/v1/tenants/${northwind.id}/members`, ({ request }) => {
    requests.push(new URL(request.url).searchParams);
    return HttpResponse.json({
      items,
      page: Number(new URL(request.url).searchParams.get("page") ?? 1),
      pageSize: 20,
      total: items.length,
    });
  });
}

/** Opens a row's ⋯ menu in the table of the given name and picks an item. */
export async function pickFromRowMenu(
  tableName: string,
  rowName: string,
  item: string,
) {
  const table = within(await screen.findByRole("table", { name: tableName }));
  await userEvent.click(
    await table.findByRole("button", { name: `Actions for ${rowName}` }),
  );
  await userEvent.click(await screen.findByRole("menuitem", { name: item }));
}
