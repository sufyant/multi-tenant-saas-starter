import type { Page } from "@playwright/test";
import {
  ErrorCode,
  type InvitationPreviewResponse,
  InvitationResponseState,
  InviterNameResponseKind,
  Language,
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
