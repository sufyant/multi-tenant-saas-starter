# @repo/emails

The emails the API sends, written with React Email: the invitation (from a member, or from the platform to a tenant's Owner), the warning before a closed tenant's purge, and the one generic notification email. It also holds the texts of the in-app notifications, which the API renders in the bell. The email boards of the design are their visual reference; this code is their source.

The API runs no Node. `pnpm generate` renders every email in every language here, and the API fills the values and sends the result (`apps/api/docs/ARCHITECTURE.md`, section 2, "Generated files", and the Notifications part of section 7).

## Where the files come from

- `src/templates/` holds one component for each email, with its subject and its plain-text part. `src/layout/` holds the layout every email shares, as the email boards draw it (13.01 to 13.15): one card of at most 562px on the muted canvas, the brand as text on its first line, and the footer last. It also holds the parts of the card (a heading, a paragraph, the one button, the button's link written out, a code), so a template gives only its words and values. Each colour comes from `packages/ui/tokens/tokens.json` (F20), light inline and dark in a `prefers-color-scheme` rule. Every cell names the system font stack: an email loads no web font and no image, so the boards' Instrument Sans and Noto Sans Arabic show only where the reader has them.
- `messages/{en,tr,ar}.json` hold the texts of each email (F14), the title and the body of every notification type, its text without each optional value, and, for a type of the email, the footer that says why the email came (`notifications`), the words of a closed list such as an entitlement key, an entitlement's value and a person the read cannot name (`notificationWords`), the button of the generic notification email (`notificationEmail`), the names of the built-in roles, and what the API needs to write a date without ICU: the month names and the patterns of a date, a moment and a time zone label. A text names its values in braces, `{tenantName}`. The Turkish and Arabic texts are drafts until a native speaker reviews them.
- `src/texts.ts` reads the languages of the contract from `@repo/api-client` (`Language`) and each one's direction from `@repo/ui` (F13), so a new language fails the type check until it has its message file.
- The brand's name and support contact come from `@repo/brand`.
- `scripts/render.tsx` writes into `apps/api/src/Modules/Notifications/Notifications.Application/Email/Generated/`, for each email and language: `<email>.<lang>.html`, `<email>.<lang>.txt` and `<email>.<lang>.subject.txt`. Each value is a placeholder, such as `{{tenantName}}`, which the API fills. It also writes `languages.json` (each language's role names and date data), `notifications.json` (each language's notification texts, with `{{name}}` placeholders, which the API renders in the bell and in the generic notification email; `apps/api/docs/ARCHITECTURE.md`, U2) and `brand.json`. It empties the folder first.

Never edit a file of the generated folder. Change a template or a message file and run `pnpm generate` (or `pnpm emails:generate`) at the repository root. Commit both. The CI job "Generated files: current" fails when the committed files are not what the script writes.

A product built from the template changes `packages/brand/brand.json` for its name and support contact, and these texts for its words.

## Dark mode

The dark colours are a `prefers-color-scheme: dark` rule over the light colours, which every element carries inline. The clients differ (caniemail.com, "prefers-color-scheme"; Litmus, "The Ultimate Guide to Dark Mode for Email Marketers"):

- Apple Mail on macOS and iOS, Outlook for macOS and Thunderbird follow the rule: the reader sees the dark email of the boards (13.08, 13.10 to 13.14).
- Gmail on the web, and most webmail, ignore the rule and show the light email.
- The Gmail apps, Outlook on Windows and Outlook.com ignore the rule and change the colours themselves: they invert light backgrounds and dark text. The email stays readable, because each text has its own colour on its own background, but its colours are the client's, not the boards'.

A client that ignores the rule shows the light email, which is complete and readable without it. No client was tried by hand in step 37a: the screenshots below come from Chromium only.

## Commands

Run from this folder, or with `pnpm --filter @repo/emails` from the root.

- `pnpm dev`: React Email's preview server on port 3002, with the samples of `src/previews/` (the design system's invented names and `.example` addresses), in each language.
- `pnpm generate`: render every email into the API.
- `pnpm screenshots`: a screenshot of each email of the email boards, in each language, light and dark, into `test-results/screenshots`, with the boards' data filled in as the API fills it. Run `pnpm generate` first. `scripts/ci/test-e2e.sh` runs it, and CI keeps the shots in the artifact `screenshots`.
- `pnpm check-types`: TypeScript, the script included.

## Tools and licenses

| Package | Use | License |
| --- | --- | --- |
| `react-email` | The components of the templates and the `email dev` command (development only) | MIT |
| `@react-email/render` | Renders a template to HTML, and formats it for review (development only) | MIT |
| `@react-email/ui` | The preview server of `email dev` (development only) | MIT |
| `react`, `react-dom` | The templates are React components (development only) | MIT |
| `tsx` | Runs the render script, which is TypeScript with JSX (development only) | MIT |
| `playwright` | Opens each email in Chromium for its screenshot (development only) | Apache-2.0 |
| `typescript`, `@types/react`, `@types/react-dom`, `@types/node` | The types (development only) | Apache-2.0, MIT |
| `@repo/api-client` | The languages of the contract (development only) | The repository's own |
| `@repo/ui` | The colour tokens and the direction of each language (development only) | The repository's own |
| `@repo/brand` | The brand's name and support contact (development only) | The repository's own |

`.github/dependency-review-config.yml` holds the licenses a dependency may have.
