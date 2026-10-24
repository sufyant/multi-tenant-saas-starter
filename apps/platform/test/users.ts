import {
  BuiltInRole,
  Language,
  type ListPageOfUserResponse,
  type UserDetailResponse,
  type UserMembershipResponse,
  UserMembershipResponseTenantStatus,
  type UserResponse,
  UserResponseStatus,
} from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
} from "@repo/api-client/msw";
import { HttpResponse, http } from "msw";
import { atlas, northwind, shannon } from "./organisations";

// Sample users of the design system's boards (Ayşe Yılmaz, Mehmet Kaya, John Byrne, Ciara Byrne, Rory Brennan) for the platform's
// tests.

const user = (
  id: string,
  firstName: string | null,
  lastName: string | null,
  email: string | null,
  values: Partial<UserResponse> = {},
): UserResponse => ({
  id,
  email,
  firstName,
  lastName,
  status: UserResponseStatus.Active,
  isSystemAdmin: false,
  isRootSystemAdmin: false,
  systemAdminGrantedAt: null,
  membershipCount: 2,
  createdAt: "2026-03-03T09:20:00Z",
  ...values,
});

export const ayse = user(
  "0198f0a2-0000-7000-8000-0000000000a1",
  "Ayşe",
  "Yılmaz",
  "ayse@northwind.example",
);
export const mehmet = user(
  "0198f0a2-0000-7000-8000-0000000000a2",
  "Mehmet",
  "Kaya",
  "mehmet@northwind.example",
);
export const john = user(
  "0198f0a2-0000-7000-8000-0000000000a3",
  "John",
  "Byrne",
  "john@northwind.example",
  { status: UserResponseStatus.Suspended, membershipCount: 1 },
);
export const ciara = user(
  "0198f0a2-0000-7000-8000-0000000000a4",
  "Ciara",
  "Byrne",
  "ciara@starter.example",
  {
    isSystemAdmin: true,
    systemAdminGrantedAt: "2026-01-05T09:00:00Z",
    membershipCount: 0,
  },
);
export const rory = user(
  "0198f0a2-0000-7000-8000-0000000000a5",
  "Rory",
  "Brennan",
  "rory@starter.example",
  {
    isSystemAdmin: true,
    isRootSystemAdmin: true,
    systemAdminGrantedAt: "2026-01-05T08:30:00Z",
    membershipCount: 0,
  },
);
/** A user a system admin deleted: no names and no email (A8). */
export const erased = user(
  "0198f0a2-0000-7000-8000-0000000000a6",
  null,
  null,
  null,
  { status: UserResponseStatus.Deleted, membershipCount: 0 },
);

export const everyUser = [ayse, mehmet, john, ciara, rory, erased];

/** One page of ListUsers. */
export const usersPageOf = (
  items: UserResponse[],
  values: Partial<ListPageOfUserResponse> = {},
): ListPageOfUserResponse => ({
  items,
  page: 1,
  pageSize: 20,
  total: items.length,
  ...values,
});

/** ListUsers: it records each request's parameters and answers the page. */
export const usersAnswer = (
  page: ListPageOfUserResponse = usersPageOf(everyUser),
  requests: URLSearchParams[] = [],
) =>
  http.get("*/v1/platform/users", ({ request }) => {
    requests.push(new URL(request.url).searchParams);
    return HttpResponse.json(page);
  });

const ownerRole = {
  id: "0198f0a2-0000-7000-8000-0000000000r1",
  name: BuiltInRole.Owner,
  builtIn: true,
};
const adminRole = {
  id: "0198f0a2-0000-7000-8000-0000000000r2",
  name: BuiltInRole.Admin,
  builtIn: true,
};

/** A membership of a user in an organisation of the list. */
export const membershipOf = (
  organisation: { id: string; name: string; slug: string },
  values: Partial<UserMembershipResponse> = {},
): UserMembershipResponse => ({
  tenantId: organisation.id,
  tenantName: organisation.name,
  tenantSlug: organisation.slug,
  tenantStatus: UserMembershipResponseTenantStatus.Active,
  role: adminRole,
  joinedAt: "2026-03-05T10:00:00Z",
  lastOwner: false,
  ...values,
});

/** Ayşe is the last Owner of Northwind Traders and an Admin of Atlas Logistics. */
export const ayseMemberships = [
  membershipOf(northwind, { role: ownerRole, lastOwner: true }),
  membershipOf(atlas, {
    tenantStatus: UserMembershipResponseTenantStatus.Suspended,
  }),
];

/** Mehmet is an Admin of Northwind Traders and holds a custom role in Shannon Studio. */
export const mehmetMemberships = [
  membershipOf(northwind),
  membershipOf(shannon, {
    role: {
      id: "0198f0a2-0000-7000-8000-0000000000r9",
      name: "Billing lead",
      builtIn: false,
    },
    joinedAt: "2026-06-10T09:00:00Z",
  }),
];

/** The answer of GetUser for a user of the list. */
export const userDetailOf = (
  value: UserResponse,
  values: Partial<UserDetailResponse> = {},
): UserDetailResponse => ({
  ...value,
  suspendedAt: null,
  suspendedBy: null,
  memberships: [],
  ...values,
});

/** GetUser: it counts the reads and answers the detail. */
export const userAnswer = (
  detail: UserDetailResponse,
  reads: { count: number } = { count: 0 },
) =>
  http.get(`*/v1/platform/users/${detail.id}`, () => {
    reads.count += 1;
    return HttpResponse.json(detail);
  });

/** GetUser that changes: it counts the reads and gives the details in order (the last one repeats). */
export const userAnswers = (
  details: UserDetailResponse[],
  reads: { count: number } = { count: 0 },
) =>
  http.get(`*/v1/platform/users/${details[0]?.id}`, () => {
    reads.count += 1;
    return HttpResponse.json(
      details[Math.min(reads.count, details.length) - 1],
    );
  });

/** A command at a path of the platform: it records each request's body and key, and gives the answers in order (the last repeats). */
export const platformCommand = (
  method: "post" | "patch" | "delete",
  path: string,
  answers: (() => Response)[],
  calls: { key: string | null; body: unknown }[] = [],
) =>
  http[method](`*/v1/platform/${path}`, async ({ request }) => {
    const text = await request.text();
    calls.push({
      key: request.headers.get("Idempotency-Key"),
      body: text ? JSON.parse(text) : null,
    });
    const answer = answers[Math.min(calls.length, answers.length) - 1];
    return answer ? answer() : HttpResponse.error();
  });

/** The UserResponse of a command, from a user of the list. */
export const userResponse =
  (value: UserResponse, values: Partial<UserResponse> = {}) =>
  () =>
    HttpResponse.json({ ...value, ...values });

/** Tomás Ryan, a system admin who is neither root nor the viewer (the SystemAdmins board). */
export const tomas = user(
  "0198f0a2-0000-7000-8000-0000000000a7",
  "Tomás",
  "Ryan",
  "tomas@starter.example",
  {
    isSystemAdmin: true,
    systemAdminGrantedAt: "2026-03-02T10:00:00Z",
    membershipCount: 0,
  },
);

/** The system admins of the SystemAdmins board: a root, the viewer (Ciara) and another. */
export const systemAdmins = [rory, ciara, tomas];

/** GetMe of the signed-in system admin as a user of the list, in English and Dublin's time. */
export const meAs = (value: UserResponse) =>
  getGetMeMockHandler(
    getGetMeResponseMock({
      id: value.id,
      firstName: value.firstName,
      lastName: value.lastName,
      email: value.email,
      isSystemAdmin: true,
      language: Language.En,
      timeZone: "Europe/Dublin",
    }),
  );
