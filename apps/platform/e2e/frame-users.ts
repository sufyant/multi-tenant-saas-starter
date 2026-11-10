import type { Page, Route } from "@playwright/test";
import {
  BuiltInRole,
  ErrorCode,
  Language,
  type UserDetailResponse,
  type UserMembershipResponse,
  UserMembershipResponseTenantStatus,
  type UserResponse,
  UserResponseStatus,
} from "@repo/api-client";
import { me, problem } from "./api";

// The Users frames (5.01 to 5.19) and the System admins frames (10.01 to 10.16): the people, the counts and the organisations they
// draw, with Deniz Kaya, the signed-in system admin of every frame, as the viewer.

const id = (n: number) =>
  `0198f0a2-0000-7000-8000-0000000c${String(n).padStart(4, "0")}`;

const person = (
  n: number,
  name: string,
  email: string,
  fields: Partial<UserResponse> = {},
): UserResponse => ({
  id: id(n),
  email,
  name,
  status: UserResponseStatus.Active,
  isSystemAdmin: false,
  isRootSystemAdmin: false,
  systemAdminGrantedAt: null,
  membershipCount: 1,
  createdAt: "2026-03-12T09:00:00Z",
  ...fields,
});

export const frameUsers = {
  ayse: person(1, "Ayşe Demir", "ayse@northwind.example", {
    membershipCount: 4,
  }),
  omar: person(2, "Omar Haddad", "omar@northwind.example"),
  grace: person(3, "Grace Okafor", "grace@northwind.example"),
  rosa: person(4, "Rosa Delgado", "rosa@pacificcoast.example"),
  aiko: person(5, "Aiko Tanaka", "aiko@blueharbour.example"),
  mark: person(6, "Mark Ellis", "mark@pacificcoast.example", {
    status: UserResponseStatus.Suspended,
    createdAt: "2026-02-03T09:00:00Z",
  }),
  nadia: person(7, "Nadia Petrov", "nadia@fernwood.example", {
    status: UserResponseStatus.Suspended,
  }),
  deniz: person(8, "Deniz Kaya", "deniz@platform.example", {
    isSystemAdmin: true,
    systemAdminGrantedAt: "2026-01-01T09:00:00Z",
    membershipCount: 0,
    createdAt: "2026-01-01T09:00:00Z",
  }),
};

/** The system admins of 10.01: Selin, the root; Deniz, the viewer; Hannah. */
export const frameAdmins = {
  selin: person(9, "Selin Arslan", "selin@platform.example", {
    isSystemAdmin: true,
    isRootSystemAdmin: true,
    systemAdminGrantedAt: "2026-01-01T08:00:00Z",
    membershipCount: 0,
  }),
  deniz: frameUsers.deniz,
  hannah: person(10, "Hannah Weber", "hannah@platform.example", {
    isSystemAdmin: true,
    systemAdminGrantedAt: "2026-09-04T09:00:00Z",
    membershipCount: 0,
  }),
};

const role = (name: BuiltInRole) => ({
  id: `0198f0a2-0000-7000-8000-0000000cf${name.length}0`,
  name,
  builtIn: true,
});

const membership = (
  n: number,
  name: string,
  roleName: BuiltInRole,
  fields: Partial<UserMembershipResponse> = {},
): UserMembershipResponse => ({
  tenantId: `0198f0a2-0000-7000-8000-0000000ca${String(n).padStart(3, "0")}`,
  tenantName: name,
  tenantSlug: name.toLowerCase().replace(/[^a-z]+/g, "-"),
  tenantStatus: UserMembershipResponseTenantStatus.Active,
  role: role(roleName),
  joinedAt: "2026-03-12T09:00:00Z",
  lastOwner: false,
  ...fields,
});

const pacificCoast = (roleName: BuiltInRole) =>
  membership(
    2,
    "Pacific Coast Freight & Warehousing Cooperative Ltd.",
    roleName,
  );

const detail = (
  user: UserResponse,
  fields: Partial<UserDetailResponse> = {},
): UserDetailResponse => ({
  ...user,
  suspendedAt: null,
  suspendedBy: null,
  suspensionReason: null,
  systemAdminGrantedBy: null,
  memberships: [],
  ...fields,
});

const details = [
  // 5.05: the only Owner of Northwind Traders, so Suspend warns (5.10) and Delete stops (5.14).
  detail(frameUsers.ayse, {
    memberships: [
      membership(1, "Northwind Traders", BuiltInRole.Owner, {
        lastOwner: true,
      }),
      pacificCoast(BuiltInRole.Admin),
      membership(3, "شركة الأفق للتجارة", BuiltInRole.Member),
      membership(4, "Blue Harbour Logistics", BuiltInRole.Member, {
        tenantStatus: UserMembershipResponseTenantStatus.Suspended,
      }),
    ],
  }),
  // 5.07: suspended by Deniz with the frame's reason.
  detail(frameUsers.mark, {
    suspendedAt: "2026-10-25T09:00:00Z",
    suspendedBy: { name: "Deniz Kaya", deleted: false },
    suspensionReason: "shared account",
    memberships: [pacificCoast(BuiltInRole.Member)],
  }),
  detail(frameUsers.deniz),
  detail(frameAdmins.selin),
  // 10.15: added by Selin.
  detail(frameAdmins.hannah, {
    systemAdminGrantedBy: { name: "Selin Arslan", deleted: false },
  }),
];

/** The ListUsers answer of the frames: a status filter, the search and the system admins, with the frame's counts (5.01). */
const listed = (route: Route, people: UserResponse[], counts = true) => {
  const params = new URL(route.request().url()).searchParams;
  const statuses = params.getAll("status");
  const q = params.get("q")?.toLocaleLowerCase("en");
  const email = params.get("email")?.toLocaleLowerCase("en");
  const items = people.filter(
    (user) =>
      (statuses.length === 0 || statuses.includes(user.status)) &&
      (!email || user.email?.toLocaleLowerCase("en") === email) &&
      (!q ||
        [user.name, user.email].some((text) =>
          text?.toLocaleLowerCase("en").includes(q),
        )),
  );
  const statusCounts = counts
    ? { active: 65, suspended: 2, deleted: 0 }
    : { active: items.length, suspended: 0, deleted: 0 };
  // The frames draw 67 users and the first page of them, so Load more stays (5.01).
  const total =
    counts && !q && statuses.length === 0
      ? 67
      : counts && statuses.includes(UserResponseStatus.Suspended)
        ? 2
        : items.length;
  return route.fulfill({
    json: { items, page: 1, pageSize: 50, total, statusCounts },
  });
};

/**
 * What the frames read: GetMe as Deniz Kaya, ListUsers with the frames' people or admins, and GetUser of each. `nextPage` answers
 * page 2 of the Users list: never (5.18) or with a failure (5.19).
 */
export const mockFrameUsers =
  ({
    nextPage,
    admins = () => Object.values(frameAdmins),
  }: {
    nextPage?: "waits" | "fails";
    admins?: () => UserResponse[];
  } = {}) =>
  async (page: Page) => {
    await page.route("**/api/v1/me", (route) =>
      route.fulfill({
        json: {
          ...me(Language.En),
          id: frameUsers.deniz.id,
          name: "Deniz Kaya",
          email: "deniz@platform.example",
        },
      }),
    );
    await page.route("**/api/v1/platform/users*", (route) => {
      if (route.request().method() !== "GET") return route.fallback();
      const params = new URL(route.request().url()).searchParams;
      if (params.get("page") === "2") {
        if (nextPage === "fails")
          return route.fulfill(problem(500, ErrorCode.ServerError));
        return new Promise(() => {});
      }
      if (params.get("isSystemAdmin") === "true")
        return listed(route, admins(), false);
      if (params.get("email"))
        return listed(
          route,
          [...Object.values(frameUsers), ...Object.values(frameAdmins)],
          false,
        );
      return listed(route, Object.values(frameUsers));
    });
    await page.route("**/api/v1/platform/users/*", (route) => {
      if (route.request().method() !== "GET") return route.fallback();
      const userId = new URL(route.request().url()).pathname.split("/").at(-1);
      const found = details.find((user) => user.id === userId);
      return found ? route.fulfill({ json: found }) : route.fallback();
    });
  };

/** Every ListUsers waits, so the page shows its loading state (5.04, 10.12). */
export const usersWait = async (page: Page) => {
  await mockFrameUsers()(page);
  await page.route("**/api/v1/platform/users*", () => new Promise(() => {}));
};

/** Every ListUsers fails (10.13). */
export const usersFail = async (page: Page) => {
  await mockFrameUsers()(page);
  await page.route("**/api/v1/platform/users*", (route) =>
    route.fulfill(problem(500, ErrorCode.ServerError)),
  );
};

/** GrantSystemAdmin for Omar: a stale second factor first (10.05), or at once (10.07). */
export const mockFrameGrant =
  (answer: "reverify" | "grant") => async (page: Page) => {
    let granted = false;
    const omar = {
      ...frameUsers.omar,
      isSystemAdmin: true,
      systemAdminGrantedAt: "2026-11-02T09:00:00Z",
    };
    await mockFrameUsers({
      admins: () => [...Object.values(frameAdmins), ...(granted ? [omar] : [])],
    })(page);
    await page.route(
      `**/api/v1/platform/users/${frameUsers.omar.id}/system-admin`,
      (route) => {
        if (answer === "reverify")
          return route.fulfill(
            problem(403, ErrorCode.SystemAdminReverificationRequired),
          );
        granted = true;
        return route.fulfill({ json: omar });
      },
    );
  };

/** RevokeSystemAdmin of Hannah answers 204 (10.11). */
export const mockFrameRevoke = async (page: Page) => {
  let revoked = false;
  await mockFrameUsers({
    admins: () =>
      Object.values(frameAdmins).filter(
        (admin) => !revoked || admin.id !== frameAdmins.hannah.id,
      ),
  })(page);
  await page.route(
    `**/api/v1/platform/users/${frameAdmins.hannah.id}/system-admin`,
    (route) => {
      revoked = true;
      return route.fulfill({ status: 204 });
    },
  );
};
