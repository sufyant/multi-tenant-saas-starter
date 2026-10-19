import type { Page } from "@playwright/test";
import {
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
  name: "Northwind MRO",
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
