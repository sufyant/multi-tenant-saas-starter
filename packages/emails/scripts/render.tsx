// Renders every email in every language of the contract into the API's Notifications module, as `pnpm generate` runs it: for each
// email and language an HTML part (`<email>.<lang>.html`), a plain-text part (`<email>.<lang>.txt`) and a subject
// (`<email>.<lang>.subject.txt`), each with a placeholder such as {{tenantName}} for every value the API fills. It also writes what the
// API needs to fill them without ICU (`languages.json`: each language's built-in role names, month names and date patterns), the texts of
// every notification type (`notifications.json`), the brand (`brand.json`) and each time zone's city in each language
// (`time-zone-cities.json`, a copy of what packages/ui generates from CLDR, so the emails name a zone as the apps do). The folder holds
// only this script's output, so the script empties it first.
import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { pretty, render } from "@react-email/render";
import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import type { ReactElement } from "react";
import {
  AccountEmail,
  type AccountEmailKind,
  type AccountEmailProps,
  accountEmailSubject,
  accountEmailText,
} from "../src/templates/account";
import {
  DeletionWarning,
  type DeletionWarningProps,
  deletionWarningSubject,
  deletionWarningText,
} from "../src/templates/deletion-warning";
import {
  Invitation,
  type InvitationProps,
  invitationSubject,
  invitationText,
} from "../src/templates/invitation";
import {
  Notification,
  type NotificationProps,
  notificationSubject,
  notificationText,
} from "../src/templates/notification";
import { format, languages, messages } from "../src/texts";

const output = new URL(
  "../../../apps/api/src/Modules/Notifications/Notifications.Application/Email/Generated/",
  import.meta.url,
);

// A placeholder for each value, in the props' own names: the API fills {{name}}. It HTML-encodes each value in the HTML part and refuses
// a link that is not an absolute https URL (apps/api/docs/ARCHITECTURE.md, section 7, "Email").
const placeholders = <T extends string>(...names: T[]) =>
  Object.fromEntries(names.map((name) => [name, `{{${name}}}`])) as Record<
    T,
    string
  >;

type Email = {
  name: string;
  html: (language: Language) => ReactElement;
  text: (language: Language) => string;
  subject: (language: Language) => string;
};

const invitation = (language: Language): InvitationProps => ({
  language,
  ...placeholders("inviterName", "tenantName", "roleName", "expiresAt", "link"),
});

const deletionWarning = (language: Language): DeletionWarningProps => ({
  language,
  ...placeholders("tenantName", "purgesOn", "purgesAt", "supportUrl"),
});

const notification = (language: Language): NotificationProps => ({
  language,
  ...placeholders("title", "body", "button", "link", "footer"),
});

// The emails about the person's own account, each under its own name, with the placeholders its kind uses.
const account =
  (
    kind: AccountEmailKind,
    values: Partial<AccountEmailProps>,
  ): ((language: Language) => AccountEmailProps) =>
  (language) => ({
    language,
    kind,
    ...values,
  });

const accountEmails: [string, (language: Language) => AccountEmailProps][] = [
  [
    "email-change-code",
    account("emailChangeCode", placeholders("verificationCode")),
  ],
  ["deletion-code", account("deletionCode", placeholders("verificationCode"))],
  [
    "email-changed",
    account("emailChanged", placeholders("newEmail", "supportUrl")),
  ],
  ["account-deleted", account("accountDeleted", {})],
];

const emails: Email[] = [
  {
    name: "invitation",
    html: (language) => <Invitation {...invitation(language)} />,
    text: (language) => invitationText(invitation(language)),
    subject: (language) => invitationSubject(invitation(language)),
  },
  {
    name: "deletion-warning",
    html: (language) => <DeletionWarning {...deletionWarning(language)} />,
    text: (language) => deletionWarningText(deletionWarning(language)),
    subject: (language) => deletionWarningSubject(deletionWarning(language)),
  },
  {
    name: "notification",
    html: (language) => <Notification {...notification(language)} />,
    text: (language) => notificationText(notification(language)),
    subject: (language) => notificationSubject(notification(language)),
  },
  ...accountEmails.map(([name, props]) => ({
    name,
    html: (language: Language) => <AccountEmail {...props(language)} />,
    text: (language: Language) => accountEmailText(props(language)),
    subject: (language: Language) => accountEmailSubject(props(language)),
  })),
];

// A notification's texts name a value as {name}; the API fills {{name}}, as in every email.
const apiPlaceholders = (text: string) =>
  text.replace(/\{(\w+)\}/g, (_, name: string) => `{{${name}}}`);

// A type's title and body, the body and the button of its email where they differ from the bell's, its texts without each optional value,
// which may keep the main title (U2), and the footer of its email, which may name a value the type always has (U5).
type NotificationTexts = {
  title?: string;
  body: string;
  email?: { body: string; button?: string };
  without?: Record<string, NotificationTexts>;
  footer?: string;
};
const apiTexts = (texts: NotificationTexts): NotificationTexts => ({
  ...(texts.title === undefined ? {} : { title: apiPlaceholders(texts.title) }),
  body: apiPlaceholders(texts.body),
  ...(texts.email === undefined
    ? {}
    : {
        email: {
          body: apiPlaceholders(texts.email.body),
          ...(texts.email.button === undefined
            ? {}
            : { button: texts.email.button }),
        },
      }),
  ...(texts.footer === undefined
    ? {}
    : { footer: apiPlaceholders(texts.footer) }),
  ...(texts.without === undefined
    ? {}
    : {
        without: Object.fromEntries(
          Object.entries(texts.without).map(([value, without]) => [
            value,
            apiTexts(without),
          ]),
        ),
      }),
});

// How a language writes an amount of money, from CLDR's data, which the browsers' Intl and so packages/ui's formatMoney also read: its group
// and decimal marks, and for each currency the text before and after the number and the digits of its minor unit. The data is a package of
// a fixed version, not the Intl of the Node that runs this script, so every machine writes the same file. A symbol that ends or starts in a
// letter next to the number takes CLDR's pattern for it ("TRY 12"). The API writes an amount from it without ICU.
type CldrNumbers = {
  main: Record<
    string,
    {
      numbers: Record<string, Record<string, string>> & {
        defaultNumberingSystem: string;
      };
    }
  >;
};
type CldrCurrencies = {
  main: Record<
    string,
    { numbers: { currencies: Record<string, { symbol?: string }> } }
  >;
};
type CldrCurrencyData = {
  supplemental: {
    currencyData: { fractions: Record<string, { _digits: string }> };
  };
};
const cldr = createRequire(import.meta.url);
const fractions = (
  cldr("cldr-core/supplemental/currencyData.json") as CldrCurrencyData
).supplemental.currencyData.fractions;
const moneyOf = (language: Language) => {
  const numbers = (
    cldr(`cldr-numbers-full/main/${language}/numbers.json`) as CldrNumbers
  ).main[language]?.numbers;
  const names = (
    cldr(`cldr-numbers-full/main/${language}/currencies.json`) as CldrCurrencies
  ).main[language]?.numbers.currencies;
  if (!numbers || !names) {
    throw new Error(`CLDR has no numbers of ${language}.`);
  }
  const marks = numbers["symbols-numberSystem-latn"] ?? {};
  const formats = numbers["currencyFormats-numberSystem-latn"] ?? {};
  const currencies = Object.fromEntries(
    Object.entries(names).map(([currency, name]) => {
      const sign = name.symbol ?? currency;
      const standard = (formats.standard ?? "¤#,##0.00").split(";")[0] ?? "";
      const before = standard.indexOf("¤") < standard.search(/[#0]/);
      const touching = before ? sign.slice(-1) : sign.slice(0, 1);
      const pattern =
        /\p{L}/u.test(touching) && formats["standard-alphaNextToNumber"]
          ? (formats["standard-alphaNextToNumber"].split(";")[0] ?? standard)
          : standard;
      const first = pattern.search(/[#0]/);
      const last = Math.max(pattern.lastIndexOf("#"), pattern.lastIndexOf("0"));
      return [
        currency,
        {
          prefix: pattern.slice(0, first).replace("¤", sign),
          suffix: pattern.slice(last + 1).replace("¤", sign),
          digits: Number(
            (fractions[currency] ?? fractions.DEFAULT)?._digits ?? "2",
          ),
        },
      ];
    }),
  );
  return { group: marks.group, decimal: marks.decimal, currencies };
};

const write = (file: string, content: string) =>
  writeFile(new URL(file, output), content, "utf8");

await rm(output, { recursive: true, force: true });
await mkdir(output, { recursive: true });

for (const email of emails) {
  for (const language of languages) {
    await write(
      `${email.name}.${language}.html`,
      await pretty(await render(email.html(language))),
    );
    await write(`${email.name}.${language}.txt`, email.text(language));
    await write(
      `${email.name}.${language}.subject.txt`,
      email.subject(language),
    );
  }
}

await write(
  "languages.json",
  `${JSON.stringify(
    Object.fromEntries(
      languages.map((language) => [
        language,
        {
          roles: messages[language].roles,
          dates: messages[language].dates,
          money: moneyOf(language),
        },
      ]),
    ),
    null,
    2,
  )}\n`,
);
// Each language's title and body of every notification type, its texts without an optional value, the footer of a type of the email, and
// the words of each closed list:
// an entitlement key, an entitlement's value, a person the read cannot name, and the forms of a number of days.
// The API renders a notification from them in the reader's language, in the bell and in the generic email (U2).
await write(
  "notifications.json",
  `${JSON.stringify(
    Object.fromEntries(
      languages.map((language) => [
        language,
        {
          types: Object.fromEntries(
            Object.entries(messages[language].notifications).map(
              ([type, texts]) => [type, apiTexts(texts)],
            ),
          ),
          words: messages[language].notificationWords,
          button: format(messages[language].notificationEmail.button, {
            brandName: brand.name,
          }),
        },
      ]),
    ),
    null,
    2,
  )}\n`,
);
await write("brand.json", `${JSON.stringify(brand, null, 2)}\n`);
await write(
  "time-zone-cities.json",
  await readFile(
    new URL("../../ui/src/lib/time-zone-cities.gen.json", import.meta.url),
    "utf8",
  ),
);
