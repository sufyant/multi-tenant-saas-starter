# Web

The tenant web of the multi-tenant SaaS starter template: the members of an organisation sign in, choose an organisation and work in it under `/[tenantSlug]/...`. The browser calls the API only through the app's own BFF route.

- [AGENTS.md](AGENTS.md): what differs in this app. [docs/frontend/AGENTS.md](../../docs/frontend/AGENTS.md) and [docs/frontend/ARCHITECTURE.md](../../docs/frontend/ARCHITECTURE.md) hold the rules of both apps.
- [.env.example](.env.example): every environment variable. `src/env.ts` checks them at start.

Run from the repository root:

```sh
pnpm --filter @repo/web dev        # http://localhost:3000; needs the API (apps/api/docs/local-run.md)
pnpm --filter @repo/web test       # Vitest: components, the BFF, the architecture rules
pnpm --filter @repo/web test:e2e   # Playwright with Clerk's test mode and axe
pnpm --filter @repo/web build
```

## Packages and licenses

| Package | Use | License |
| --- | --- | --- |
| `next`, `react`, `react-dom` | The app | MIT |
| `@clerk/nextjs`, `@clerk/ui` | Sign-in and the session token that the BFF sends | MIT |
| `@repo/api-client`, `@repo/ui` | The generated client and the components | The repository's own |
| `@tanstack/react-query` | Server data in the browser (F8) | MIT |
| `next-intl` | Messages and the locale (F13) | MIT |
| `nuqs` | Filters, tabs, paging and sort in the URL (F9) | MIT |
| `next-themes` | Light, dark and system (F24) | MIT |
| `zod` | `src/env.ts` and the Zod error map (F7, F12) | MIT |
| `vitest`, `jsdom`, `@vitejs/plugin-react`, `vite-tsconfig-paths` | The unit tests (development only) | MIT |
| `@testing-library/react`, `@testing-library/dom`, `@testing-library/user-event`, `@testing-library/jest-dom` | The component tests (development only) | MIT |
| `msw` | The API in the unit tests (development only) | MIT |
| `@playwright/test`, `@clerk/testing` | The browser tests (development only) | Apache-2.0, MIT |
| `@axe-core/playwright` | WCAG 2.2 AA in the browser tests (development only, F37) | MPL-2.0, allowed by name in `.github/dependency-review-config.yml` |
| `tailwindcss`, `@tailwindcss/postcss`, `typescript`, `@types/*` | The build (development only) | MIT, Apache-2.0 |
