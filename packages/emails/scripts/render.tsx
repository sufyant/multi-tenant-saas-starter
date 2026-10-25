// Renders every email in every language of the contract into the API's Notifications module, as `pnpm generate` runs it: for each
// email and language an HTML part (`<email>.<lang>.html`), a plain-text part (`<email>.<lang>.txt`) and a subject
// (`<email>.<lang>.subject.txt`), each with a placeholder such as {{tenantName}} for every value the API fills. It also writes what the
// API needs to fill them without ICU (`languages.json`: each language's built-in role names, month names and date patterns) and the
// brand (`brand.json`). The folder holds only this script's output, so the script empties it first.
import { mkdir, rm, writeFile } from "node:fs/promises";
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
import { languages, messages } from "../src/texts";

const output = new URL(
  "../../../apps/api/src/Modules/Notifications/Notifications.Application/Email/Generated/",
  import.meta.url,
);

// A placeholder for each value, in the props' own names: the API fills {{name}}.
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
];

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
await write("brand.json", `${JSON.stringify(brand, null, 2)}\n`);
