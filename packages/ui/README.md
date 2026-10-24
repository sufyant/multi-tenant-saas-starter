# @repo/ui

The look and the shared parts of every frontend app: the design tokens of the Starter design system, the global CSS, the 20 components, the parts both apps share, the date formatter, the language helpers, the Zod error map, the shared messages and the architecture rules. Rules F20 to F37 in `docs/frontend/ARCHITECTURE.md` say what each part must do.

## Where the files come from

- `tokens/tokens.json` is the source of every design token. It is a copy of `tokens.json` of the Starter design system. A product keeps the token names and may change the values.
- `scripts/build-tokens.mjs` writes `src/styles/tokens.gen.css` from it: light colours and shadows on `:root`, dark ones on `.dark`, every other token on `:root`, and the breakpoints in a Tailwind `@theme` block (F20).
- `src/styles/globals.css` imports `tokens.gen.css` and maps the tokens into Tailwind's theme, so `bg-primary`, `text-muted-foreground`, `rounded-md`, `h-control-md`, `shadow-md` and `tablet:` work.
- `src/components/<name>/` holds each of the 20 components of the design system's `components/index.d.ts`, with its test and an `index.ts`: `app-shell`, `banner`, `button`, `combobox`, `dialog`, `empty-state`, `freshness`, `icon`, `input`, `org-menu`, `row-menu`, `skeleton`, `status`, `sub-nav`, `table`, `tabs`, `tag`, `time`, `toast`, `user-menu`. The README of each component in the design system is its specification. Import one as `@repo/ui/components/button`.
- `src/components/` also holds the parts of our own sign-in, which both apps share (step 21b): `code-input`, `provider-buttons`, `auth-layout` and `sign-in` (`SignIn`, its email, code and second-factor steps, `SsoCallback`, the Clerk error map and the redirect rule). They are not components of the design system; the D5 boards are their specification. `src/components/preferences-form/` holds the Preferences form of both apps (step 22e, the T11 and P7 boards): the name, the language and the time zone, with one Save; each app gives it the generated schema and calls. Only `sign-in` imports Clerk; `sign-in`, `preferences-form`, `list-error`, `src/lib/use-failure-text.ts` and the dev page import next-intl.
- `src/components/` holds the parts both apps share since step 22k (F29): `list-error` (`ListError`, a list that could not load), `search` (`Search`, a list's search field), `avatar` (`Avatar` and `initialsOf`, on the shadcn `Avatar`); `src/lib/use-failure-text.ts` holds `useFailureText`. `ListError` and `useFailureText` take the failed call that `problemOf` of `@repo/api-client` reads, its code and its reference, so this package reads no generated client at run time.
- `src/components/ui/` holds the shadcn/ui primitives on Base UI (`components.json`: `base-nova`, `rtl`). Add one with `pnpm dlx shadcn@<version> add <name>` from this folder; then point its imports at `./`, `../../lib/utils` and `../icon/icons`, and pass every text as a prop. `checkbox` and `radio-group` (step 22k, F52) render a native button, so a disabled control carries `disabled`; each has its test beside it.
- `src/components/dev-components-page.tsx` renders every component for `/dev/components` (F33).
- `src/lib/rail-state.ts` names the rail's cookie and reads its value, for `AppShell` and each app's layout (F49).
- `src/lib/format-date-time.ts` is the one formatter of a date or a time (F18); `src/lib/locale.ts` chooses the language of a request and its direction (F13); `src/lib/zod-error-map.ts` turns a Zod issue into a message key (F12).
- `messages/{en,tr,ar}.json` hold the shared texts: buttons, themes, language names, validation, problem screens and one message for each error code of the API (`errors.<code>`, F16). The apps merge them with their own messages at load (F14).
- `test/architecture-rules.ts` holds the architecture rules that Biome cannot check, as pure functions of a folder (F36). `src/architecture.test.ts` runs them on this package; each app's `architecture.test.ts` runs them on the app.

Never edit `src/styles/tokens.gen.css`. Change `tokens/tokens.json` and run `pnpm generate` (or `pnpm ui:tokens`) at the repository root. Commit both files. The CI job `generated` fails when the committed CSS is not what the script writes.

## Commands

Run from this folder, or with `pnpm --filter @repo/ui` from the root.

- `pnpm generate:tokens`: write `src/styles/tokens.gen.css`.
- `pnpm test`: the components' tests, the token script's tests, the rules' tests and the architecture tests of this package.
- `pnpm check-types`: TypeScript, the scripts included.

## Tools and licenses

| Package | Use | License |
| --- | --- | --- |
| `@base-ui/react` | The primitives of the components | MIT |
| `@tanstack/react-table` | The `Table` component, in manual mode (F10) | MIT |
| `cmdk` | The command palette of `AppShell` (F28) | MIT |
| `input-otp` | The six slots of `CodeInput`, through shadcn's `InputOTP` | MIT |
| `@clerk/nextjs` | The hooks of `SignIn` and `SsoCallback` (Clerk's custom flows, Core 3). A peer: the apps give it, so one Clerk context serves the app and `SignIn` | MIT |
| `next-intl` | The texts of `SignIn`, its steps and `PreferencesForm`, from the shared messages. A peer: the apps give it | MIT |
| `react-hook-form` | The state and the validation of `PreferencesForm` (F11) | MIT |
| `@hookform/resolvers` | The Zod resolver of `PreferencesForm`, for the schema the app gives (F11) | MIT |
| `sonner` | The `Toast` component (F23) | MIT |
| `next-themes` | The theme of `Toaster` and of the dev page (F24) | MIT |
| `lucide-react` | The icons, only through the `Icon` component (F22) | ISC |
| `class-variance-authority` | The variants of a component | Apache-2.0 |
| `clsx`, `tailwind-merge` | `cn`, which joins class names | MIT |
| `tw-animate-css` | The animation utilities of shadcn/ui | MIT |
| `zod` | The type of the Zod error map | MIT |
| `tailwindcss` | The CSS framework (development only) | MIT |
| `vitest`, `jsdom`, `@vitejs/plugin-react` | The tests (development only) | MIT |
| `@testing-library/react`, `@testing-library/dom`, `@testing-library/user-event`, `@testing-library/jest-dom` | The component tests (development only) | MIT |
| `react`, `react-dom` | The tests; the apps give them as peers (development only) | MIT |
| `axe-core` | The axe check of a component's open state in jsdom, the engine of the apps' Playwright check (F37; development only) | MPL-2.0, allowed by name for the tests (F38) |
| `typescript` | The types, and the parser of the architecture rules (development only) | Apache-2.0 |
| `@types/node` | The types of Node, for the token script and the architecture rules (development only) | MIT |
| `@repo/api-client` | The architecture tests read its generated enums (development only) | The repository's own |

`.github/dependency-review-config.yml` holds the licenses a dependency may have.
