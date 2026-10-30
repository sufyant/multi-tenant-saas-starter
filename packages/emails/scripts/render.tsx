// Renders every email in every language of the contract into the API's Notifications module, as `pnpm generate` runs it: for each
// email and language an HTML part (`<email>.<lang>.html`), a plain-text part (`<email>.<lang>.txt`) and a subject
// (`<email>.<lang>.subject.txt`), each with a placeholder such as {{tenantName}} for every value the API fills. It also writes what the
// API needs to fill them without ICU (`languages.json`: each language's built-in role names, month names and date patterns), the texts of
// every notification type (`notifications.json`) and the brand (`brand.json`). The folder holds only this script's output, so the script empties it first.
// Last, it writes the notification categories' names into the shared messages of `packages/ui`.
import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
import { pretty, render } from "@react-email/render";
import type { Language } from "@repo/api-client";
import { brand } from "@repo/brand";
import type { ReactElement } from "react";
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
import { languages, messages } from "../src/texts";

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
  ...placeholders("title", "body", "link", "footer"),
});

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
];

// A notification's texts name a value as {name}; the API fills {{name}}, as in every email.
const apiPlaceholders = (text: string) =>
  text.replace(/\{(\w+)\}/g, (_, name: string) => `{{${name}}}`);

// A type's title and body, and its texts without each optional value, which may keep the main title (U2).
type NotificationTexts = {
  title?: string;
  body: string;
  without?: Record<string, NotificationTexts>;
};
const apiTexts = (texts: NotificationTexts): NotificationTexts => ({
  ...(texts.title === undefined ? {} : { title: apiPlaceholders(texts.title) }),
  body: apiPlaceholders(texts.body),
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
        { roles: messages[language].roles, dates: messages[language].dates },
      ]),
    ),
    null,
    2,
  )}\n`,
);
// Each language's title and body of every notification type, its texts without an optional value, and the words of each closed list:
// an entitlement key, an entitlement's value and a person the read cannot name; each category's name and description; and the two footers
// of the generic email: a category's, with its name, and a required type's.
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
          categories: messages[language].notificationCategories,
          footers: {
            category: apiPlaceholders(
              messages[language].notificationEmail.footer,
            ),
            required: messages[language].notificationEmail.requiredFooter,
          },
        },
      ]),
    ),
    null,
    2,
  )}\n`,
);
await write("brand.json", `${JSON.stringify(brand, null, 2)}\n`);

// The name and the description of each notification category, which the preferences form of packages/ui lists: this script writes them
// under `notificationCategories` in the shared messages of packages/ui (F14), so they are written once, here (D1). Every other key of
// those files stays as it is.
for (const language of languages) {
  const file = new URL(`../../ui/messages/${language}.json`, import.meta.url);
  const shared = JSON.parse(await readFile(file, "utf8"));
  shared.notificationCategories = messages[language].notificationCategories;
  await writeFile(file, `${JSON.stringify(shared, null, 2)}\n`, "utf8");
}
