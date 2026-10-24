import {
  type ErrorCode,
  type FieldErrorCode,
  InvitationResponseState,
  Language,
  type ListPageOfTenantListItemResponse,
  type TenantDetailResponse,
  type TenantListItemResponse,
  TenantResponseStatus,
} from "@repo/api-client";
import {
  getGetMeMockHandler,
  getGetMeResponseMock,
} from "@repo/api-client/msw";
import { formatDate } from "@repo/ui/lib/format-date-time";
import { HttpResponse, http } from "msw";

// Sample organisations of the design system's boards (Northwind Traders, Atlas Logistics, Kestrel Labs) for the platform's tests.

export const day = 24 * 60 * 60 * 1000;
/** The date a sentence shows, as the screen formats it in English and Dublin's time (F18). */
export const dateOf = (at: string) =>
  formatDate(at, { locale: "en-IE", timeZone: "Europe/Dublin" });

export const inDays = (days: number) =>
  new Date(Date.now() + days * day).toISOString();

const item = (
  id: string,
  name: string,
  slug: string,
  values: Partial<TenantListItemResponse> = {},
): TenantListItemResponse => ({
  id,
  name,
  slug,
  status: TenantResponseStatus.Active,
  createdAt: inDays(-90),
  suspensionReason: null,
  closedAt: null,
  purgedAt: null,
  reopenableUntil: null,
  memberCount: 7,
  ownerCount: 1,
  ownerInvitation: null,
  ...values,
});

export const northwind = item(
  "0198f0a2-0000-7000-8000-000000000001",
  "Northwind Traders",
  "northwind",
  { createdAt: "2026-03-03T09:14:00Z" },
);
export const shannon = item(
  "0198f0a2-0000-7000-8000-000000000003",
  "Shannon Studio",
  "shannon-studio",
  { ownerCount: 3, memberCount: 14 },
);
export const liffey = item(
  "0198f0a2-0000-7000-8000-000000000004",
  "Liffey Design",
  "liffey-design",
  {
    ownerCount: 0,
    memberCount: 0,
    ownerInvitation: {
      email: "niamh@liffeydesign.example",
      expiresAt: inDays(7),
      state: InvitationResponseState.Pending,
      language: Language.Tr,
    },
  },
);
export const burren = item(
  "0198f0a2-0000-7000-8000-000000000002",
  "Burren Supply",
  "burren-supply",
  {
    ownerCount: 0,
    memberCount: 0,
    ownerInvitation: {
      email: "orla@burren.example",
      expiresAt: inDays(-7),
      state: InvitationResponseState.Expired,
      language: Language.En,
    },
  },
);
export const atlas = item(
  "0198f0a2-0000-7000-8000-000000000005",
  "Atlas Logistics",
  "atlas-logistics",
  {
    status: TenantResponseStatus.Suspended,
    suspensionReason: "Contract under review",
    memberCount: 9,
  },
);
export const kestrel = item(
  "0198f0a2-0000-7000-8000-000000000006",
  "Kestrel Labs",
  "kestrel",
  {
    status: TenantResponseStatus.Closed,
    closedAt: inDays(-27),
    reopenableUntil: inDays(3),
    memberCount: 4,
  },
);
export const moyne = item(
  "0198f0a2-0000-7000-8000-000000000007",
  "Moyne Gallery",
  "moyne-gallery",
  {
    status: TenantResponseStatus.Purging,
    closedAt: inDays(-30),
    memberCount: 5,
  },
);
export const corrib = item(
  "0198f0a2-0000-7000-8000-000000000008",
  "Corrib Press",
  "corrib-press",
  {
    status: TenantResponseStatus.Purged,
    createdAt: "2025-11-04T14:00:00Z",
    purgedAt: inDays(-7),
    memberCount: 0,
    ownerCount: 0,
  },
);

export const everyOrganisation = [
  northwind,
  shannon,
  liffey,
  burren,
  atlas,
  kestrel,
  moyne,
  corrib,
];

/** One page of ListTenants. */
export const pageOf = (
  items: TenantListItemResponse[],
  values: Partial<ListPageOfTenantListItemResponse> = {},
): ListPageOfTenantListItemResponse => ({
  items,
  page: 1,
  pageSize: 20,
  total: items.length,
  ...values,
});

/** ListTenants: it records each request's parameters and answers the page. */
export const listAnswer = (
  page: ListPageOfTenantListItemResponse = pageOf(everyOrganisation),
  requests: URLSearchParams[] = [],
) =>
  http.get("*/v1/platform/tenants", ({ request }) => {
    requests.push(new URL(request.url).searchParams);
    return HttpResponse.json(page);
  });

/** The answer of GetTenant for an organisation of the list. */
export const detailOf = (
  organisation: TenantListItemResponse,
  values: Partial<TenantDetailResponse> = {},
): TenantDetailResponse => ({
  ...organisation,
  purgeWarningAt: null,
  owners:
    organisation.ownerCount > 0
      ? [
          {
            userId: "0198f0a2-0000-7000-8000-0000000000a1",
            firstName: "Ayşe",
            lastName: "Yılmaz",
            email: "ayse@northwind.example",
          },
        ]
      : [],
  ...values,
});

/** GetTenant: it counts the reads and answers the detail. */
export const detailAnswer = (
  detail: TenantDetailResponse,
  reads: { count: number } = { count: 0 },
) =>
  http.get(`*/v1/platform/tenants/${detail.id}`, () => {
    reads.count += 1;
    return HttpResponse.json(detail);
  });

/** GetMe of the signed-in system admin, in English and Dublin's time. */
export const meAnswer = (language: Language = Language.En) =>
  getGetMeMockHandler(
    getGetMeResponseMock({
      isSystemAdmin: true,
      language,
      timeZone: "Europe/Dublin",
    }),
  );

/** A Problem Details answer of the API. */
export const problem = (
  status: number,
  code: ErrorCode,
  errors?: Record<string, FieldErrorCode[]>,
  tenantIds?: string[],
) =>
  HttpResponse.json(
    {
      status,
      title: "Refused",
      code,
      traceId: "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
      ...(errors ? { errors } : {}),
      ...(tenantIds ? { tenantIds } : {}),
    },
    { status },
  );

/** A command of an organisation: it records each request's body and key, and gives the answers in order (the last one repeats). */
export const commandAnswer = (
  method: "post" | "patch",
  path: string,
  answers: (() => Response)[],
  calls: { key: string | null; body: unknown }[] = [],
) =>
  http[method](`*/v1/platform/tenants/${path}`, async ({ request }) => {
    const text = await request.text();
    calls.push({
      key: request.headers.get("Idempotency-Key"),
      body: text ? JSON.parse(text) : null,
    });
    const answer = answers[Math.min(calls.length, answers.length) - 1];
    return answer ? answer() : HttpResponse.error();
  });

/** The TenantResponse of a command, from an organisation of the list. */
export const tenantAnswer = (
  organisation: TenantListItemResponse,
  values: Partial<TenantListItemResponse> = {},
) => {
  const { id, name, slug, status, suspensionReason, closedAt, purgedAt } = {
    ...organisation,
    ...values,
  };
  return () =>
    HttpResponse.json({
      id,
      name,
      slug,
      status,
      suspensionReason,
      closedAt,
      purgedAt,
    });
};
