import type { Page } from "@playwright/test";
import { Language, type MeResponse } from "@repo/api-client";

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
  language: Language = Language.en,
): Promise<void> {
  await page.route("**/api/v1/me", (route) =>
    route.fulfill({ json: me(language) }),
  );
}
