# AGENTS.md

This is the frontend of a multi-tenant SaaS starter template: two Next.js apps (`apps/web` for the members of an organisation, `apps/platform` for the system admins) and two packages (`packages/api-client`, `packages/ui`). Every future product is built on it, so one source for each piece of knowledge, three languages and clear checks matter more than speed. The text below holds for both apps. Each app's own `AGENTS.md` adds only what differs.

## Source of truth

- `docs/frontend/ARCHITECTURE.md` holds every frontend rule, each with an id (`F6`, `F17`). It is authoritative. If this file and it disagree, it wins.
- The API's contract is `apps/api/openapi/v1.json`, and its rules are in `apps/api/docs/ARCHITECTURE.md` and `apps/api/docs/api-catalog.md` (K5 the contract, K7 the permission names, K8 language and time zone). The frontend never contradicts them.
- The look is the Starter design system: `packages/ui/tokens/tokens.json` and the README of each component, which is its specification.
- Cite a rule by its id. Do not copy a rule into a code comment.

## Commands

Run from the repository root.

- Install: `pnpm install`.
- Types, tests and build of the frontend: `pnpm turbo run check-types test build '--filter=!@repo/api'`.
- Lint and format: `pnpm lint`, `pnpm format`.
- Run every generator after a change to `v1.json`, `tokens.json` or another source of generated code: `pnpm generate`. It writes the token CSS, the API's generated files, `v1.json` and the client, in that order. Commit what it changes; the CI job `generated` fails otherwise (D1, F32).
- Only the token CSS: `pnpm ui:tokens`. Only the client: `pnpm api:generate`.
- One app in development: `pnpm --filter @repo/web dev` (port 3000), `pnpm --filter @repo/platform dev` (port 3001).
- Playwright: `pnpm --filter @repo/web test:e2e`.

## Closed decisions

These were decided with the maintainer in step 21a, with their sources. Do not reopen one, and do not propose to, unless the maintainer asks or you have new information. Then say what the new information is.

- Orval generates the client: types, Zod schemas, query options and MSW handlers. One package for every app. (F1)
- Screens use the generated query options with `useQuery`, never the generated hooks. (F2)
- Tables are TanStack Table v9 in manual mode, inside our `Table`. Forms are React Hook Form with the generated Zod schemas. URL state is nuqs. There is no global store. (F8 to F11)
- next-intl without i18n routing; the locale comes from a cookie. Dates use the Intl API only, in one file. (F13, F18)
- The BFF: the browser never calls the API and never holds a token. (F6)
- The tenant web's routes are `/[tenantSlug]/...`; the API path keeps the id. (F30)
- Feature folders and thin routes. (F29)
- `tokens.json` is the source of the look. (F20)
- A rule gets a Biome rule, else a Vitest architecture test. No second linter. No Storybook. (F34, F36)
- shadcn on Base UI, never Radix. A component uses `render`, never `asChild`.
- No commercially licensed package. A new package needs a license check and a row in its package's README.

## Known traps

- This Next.js is 16: `middleware.ts` is `proxy.ts`, and the guides in `node_modules/next/dist/docs/` win over what you remember. Read the guide before you write Next.js code.
- Orval writes a hook beside each query's options. Biome refuses a `use...` import from `@repo/api-client`; call `useQuery(getXQueryOptions(...))`. (F2)
- Orval names an enum member from the `x-enumNames` of the API's document: `ErrorCode.UserSuspended` is `user.suspended`. Compare with the member, never with a string. (F3, N11z)
- The generated functions call `/v1/...`, and the mutator puts `configureApiClient`'s base address in front. In the apps the base is `/api`, the BFF. A test in Node needs an absolute base, such as `https://web.example.test/api`. (F1)
- MSW 3 names the option `onUnhandledFrame`, not `onUnhandledRequest`.
- A token whose name Tailwind also uses (`radius-md`, `shadow-md`, `font-sans`) keeps its name; `tokens.gen.css` is outside every cascade layer, so it wins. A shadow is named in `@theme inline` too, because Tailwind writes its default shadow into the utility. (F20)
- A breakpoint cannot be a variable in a media query, so `tokens.gen.css` writes the breakpoints into an `@theme` block as values. (F20)
- `next build` cleans `.next` while `next typegen` writes to it. In `turbo.json`, the web `build` depends on its own `check-types`.
- The password strategy is off in Clerk (`.clerk/config.json`). Playwright signs in through our own screens (F41) with a `+clerk_test` address and the code `424242`. The web needs `E2E_CLERK_USER_EMAIL` and the development keys in the app's `.env.local`. The platform's setup makes its own temporary user with `CLERK_SECRET_KEY`, enrols its authenticator app and deletes it in the teardown (F42). In CI, the `e2e` job reads them from the repository secrets of `docs/github-settings.md`, section 3.7, and runs the production build, where the dev-components test expects 404.
- Clerk's custom flows are Core 3 in `@clerk/nextjs` 7: `useSignIn().signIn.emailCode.sendCode`, `signIn.mfa.verifyTOTP`, `signIn.finalize`, `HandleSSOCallback`. A method returns `{ error }`; the code of an API error is in `error.errors[0].code` (`clerkErrorCode`). The user's and the session's methods (`createTOTP`, `attemptSecondFactorVerification`) still throw.
- jsdom has no `elementFromPoint`; input-otp calls it, so `test/setup.ts` stubs it too.
- jsdom has no `matchMedia`, `ResizeObserver` or `scrollIntoView`; `test/setup.ts` of each package and app stubs them, because next-themes, sonner and cmdk read them.
- `/dev/components` is a `page.dev.tsx`. `next.config.ts` adds the page extension `dev.tsx` only outside production, so `next build` has no such route and `next dev` has it.
- A value of a contract enum is never a literal, even when it is a word of our own: the action of a suspended user is `account-suspended`, not `suspended`, which `TenantResponseStatus` holds.
- A command palette in shadcn on Base UI puts `Command` inside `CommandDialog`; without it, cmdk throws.
- The apps read their variables at start (`instrumentation.ts`), so an image needs every variable of `.env.example` to start.

## How we design

Reason from these principles. When unsure, ask what the sources in the reading list would say.

- One source for each piece of knowledge. Generate it or read it from the generated file. (Hunt and Thomas)
- Simplicity first. Use what Next.js, TanStack Query, Base UI and the Intl API give. Add capability for a real need. (Ousterhout)
- The screen speaks the user's language: "organisation", "Activity", "Platform". The code speaks the API's: `tenant`, `audit`, `platform`. (Evans)
- Every language is first class. Logical properties, no text in code, Western digits. (W3C Internationalization)
- Check permissions, not roles. The UI hides what the user may not do; the API decides.
- The browser holds no secret. (RFC 10017)
- A check guards every rule that can be checked. (Ford, Parsons, Kua)

## Decisions

If a task needs a decision that `docs/frontend/ARCHITECTURE.md` does not cover, or contradicts one, stop. Do not decide silently.

- Give two or three options. For each: what it costs, and its source (book and chapter, or named industry guidance).
- Recommend one, once. Change the recommendation only for new information, and say what it is.
- A deviation from the books goes into the deviations table of `docs/frontend/ARCHITECTURE.md`, with the reason. Propose the change; do not accept it yourself.

## How we work

- Test first. A feature starts from a failing test at the outer boundary. A fix starts from a red test that checks the rule. (Beck; Freeman and Pryce)
- Branch from `main`: `feat/<name>`, `fix/<name>` or `docs/<name>`. Small commits: the test, then the change, then the documents. Open a pull request; do not merge it.
- The pull request description is a report: what changed for each item, the red run of each new test, the test count before and after by package, every package installed or raised with its version, and every choice a reviewer could question.
- Before you open the pull request, run the `architecture-reviewer` subagent and fix what it finds.

## How we test

The rules are in section 6 of `docs/frontend/ARCHITECTURE.md`. In short:

- Vitest and Testing Library, beside the file they test. Find an element by its role and name.
- MSW handlers from `@repo/api-client/msw`. Never fake our own code; fake the API at the network.
- Playwright with `@clerk/testing`, and axe on every page (WCAG 2.2 AA).
- Test names: `Operation_Scenario_ExpectedOutcome`; an architecture test `Rule_Scope_Expectation`. No `if`, loop, `?:` or `??` in a test body.
- Fix a flaky test at its root; never add a retry.

## Writing

- Code, comments, documents and commit messages are in English. The message files hold English, Turkish and Arabic.
- Documents follow ASD-STE100: short sentences, active voice, one idea per sentence. No em dashes.
- A document in `docs/` has one shape: a level 1 title, the purpose, numbered level 2 sections (`## 1. Name`), no date of its last change.
- Copy follows the design system's README: sentence case, a button starts with a verb, an error says what went wrong and what to do next, no apology.
- No secrets in the repository. Configuration comes from the environment.

## Definition of done

- `pnpm lint` passes. Types, tests and build pass with no new warning.
- New behaviour is covered by tests written first.
- Every component works in light and dark, left to right and right to left, and at the three control heights.
- No generated file was edited by hand, and `pnpm generate` leaves a clean tree.
- `docs/frontend/ARCHITECTURE.md` says what the code does.

## Reading list

| Area | Sources |
| --- | --- |
| Design | Ousterhout, *A Philosophy of Software Design*; Hunt and Thomas, *The Pragmatic Programmer* |
| Architecture | Ford, Parsons, Kua, *Building Evolutionary Architectures*; Robert C. Martin, "Screaming Architecture"; bulletproof-react |
| Data and state | TanStack Query documentation ("Advanced Server Rendering", "Query Options"); TanStack Table documentation |
| Security | IETF, "OAuth 2.0 for Browser-Based Applications" (RFC 10017, BCP 212); OWASP ASVS |
| Languages and time | W3C Internationalization ("Language tags in HTML and XML", "Structural markup and right-to-left text in HTML", "Working with time zones"); ECMA-402 |
| Accessibility | W3C, WCAG 2.2; Testing Library, "Guiding Principles" |
| Testing | Freeman and Pryce, *Growing Object-Oriented Software, Guided by Tests*; Khorikov, *Unit Testing Principles, Practices, and Patterns* |
