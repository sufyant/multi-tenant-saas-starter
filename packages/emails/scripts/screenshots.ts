// Takes a screenshot of every email that the email boards draw (13.01 to 13.15), in every language and in the light and the dark theme,
// for the review against the design. It reads the generated parts of the API's folder, fills each placeholder with the board's data as
// the API fills it (each value HTML-encoded), and opens the result in Chromium at the boards' width. The shots go into
// test-results/screenshots, which ci.yml uploads with the apps' shots; they never go into the repository. Run `pnpm generate` first.
import { mkdir, readFile, rm } from "node:fs/promises";
import type { Language } from "@repo/api-client";
import { chromium } from "playwright";
import { languages } from "../src/texts";

const generated = new URL(
  "../../../apps/api/src/Modules/Notifications/Notifications.Application/Email/Generated/",
  import.meta.url,
);
const output = new URL("../test-results/screenshots/", import.meta.url);

const read = async (file: string) => readFile(new URL(file, generated), "utf8");
const json = async <T>(file: string): Promise<T> =>
  JSON.parse(await read(file)) as T;

type Dates = {
  months: string[];
  date: string;
  moment: string;
  zone: string;
};
type LanguageData = { roles: Record<string, string>; dates: Dates };
type TypeTexts = { title: string; body: string; footer: string };

const languageData =
  await json<Record<Language, LanguageData>>("languages.json");
const notificationTexts =
  await json<Record<Language, { types: Record<string, TypeTexts> }>>(
    "notifications.json",
  );
const cities = await json<Record<string, Record<Language, string>>>(
  "time-zone-cities.json",
);

const fill = (text: string, values: Record<string, string>) =>
  text.replace(/\{\{?(\w+)\}?\}/g, (_, name: string) => {
    const value = values[name];
    if (value === undefined) {
      throw new Error(`No value for ${name} in "${text}".`);
    }
    return value;
  });

const encode = (value: string) =>
  value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");

// A day and a moment as EmailDates writes them: the boards' days in November and December 2026, at 09:00 in Istanbul (UTC+3).
const day = (language: Language, date: number, month: number) =>
  fill(languageData[language].dates.date, {
    day: String(date),
    month: languageData[language].dates.months[month] ?? "",
    year: "2026",
  });
const moment = (language: Language, date: number, month: number) =>
  fill(languageData[language].dates.moment, {
    date: day(language, date, month),
    time: "09:00",
    zone: fill(languageData[language].dates.zone, {
      city: cities["Europe/Istanbul"]?.[language] ?? "Istanbul",
      offset: "UTC+3",
    }),
  });

const webApp = "https://app.starter.example/";

// A notification email as SendNotificationEmailHandler writes it: the type's title, body and footer with its values.
const notification = (
  language: Language,
  type: string,
  values: Record<string, string>,
) => {
  const texts = notificationTexts[language].types[type];
  if (!texts) {
    throw new Error(`No texts of ${type}.`);
  }
  return {
    title: fill(texts.title, values),
    body: fill(texts.body, values),
    link: webApp,
    footer: fill(texts.footer, values),
  };
};

type Shot = {
  frame: string;
  email: string;
  values: (language: Language) => Record<string, string>;
};

const shots: Shot[] = [
  {
    frame: "13.01-invitation",
    email: "invitation",
    values: (language) => ({
      inviterName: "Ayşe Demir",
      tenantName: "Northwind Traders",
      roleName: languageData[language].roles.admin ?? "",
      expiresAt: moment(language, 8, 10),
      link: `${webApp}invitations/accept?token=7f3c9a`,
    }),
  },
  {
    frame: "13.03-removed",
    email: "notification",
    values: (language) =>
      notification(language, "member.removed", {
        tenantName: "Northwind Traders",
        actorName: "Omar Haddad",
      }),
  },
  {
    frame: "13.04-data-file-ready",
    email: "notification",
    values: (language) =>
      notification(language, "user.data_export_ready", {
        expiresAt: day(language, 8, 10),
      }),
  },
  {
    frame: "13.05-role-changed",
    email: "notification",
    values: (language) =>
      notification(language, "member.role_changed", {
        tenantName: "Northwind Traders",
        actorName: "Omar Haddad",
        oldRoleName: languageData[language].roles.member ?? "",
        newRoleName: languageData[language].roles.admin ?? "",
      }),
  },
  {
    frame: "13.06-organisation-closed",
    email: "notification",
    values: (language) =>
      notification(language, "tenant.closed_for_owners", {
        tenantName: "Northwind Traders",
        reopenableUntil: day(language, 1, 11),
      }),
  },
  {
    frame: "13.07-deletion-warning",
    email: "deletion-warning",
    values: (language) => ({
      tenantName: "Northwind Traders",
      purgesOn: day(language, 1, 11),
      purgesAt: moment(language, 1, 11),
      supportUrl: "mailto:support@starter.example",
    }),
  },
];

await rm(output, { recursive: true, force: true });
await mkdir(output, { recursive: true });

const browser = await chromium.launch();
try {
  for (const colorScheme of ["light", "dark"] as const) {
    // The boards are 642px wide: the card of 562px on 40px of canvas at each side. The page is low, so the full-page shot is as high as
    // the email.
    const page = await browser.newPage({
      viewport: { width: 642, height: 100 },
      colorScheme,
    });
    for (const shot of shots) {
      for (const language of languages) {
        const values = shot.values(language);
        const html = fill(
          await read(`${shot.email}.${language}.html`),
          Object.fromEntries(
            Object.entries(values).map(([name, value]) => [
              name,
              encode(value),
            ]),
          ),
        );
        await page.setContent(html, { waitUntil: "load" });
        await page.screenshot({
          path: new URL(
            `emails-${shot.frame}-${language}-${colorScheme}.png`,
            output,
          ).pathname,
          fullPage: true,
        });
      }
    }
    await page.close();
  }
} finally {
  await browser.close();
}
