# Platform

The provider's app of the multi-tenant SaaS starter template: the system admins sign in and manage every organisation and user. On screen it is "Platform". The browser calls the API only through the app's own BFF route.

- [AGENTS.md](AGENTS.md): what differs in this app. [docs/frontend/AGENTS.md](../../docs/frontend/AGENTS.md) and [docs/frontend/ARCHITECTURE.md](../../docs/frontend/ARCHITECTURE.md) hold the rules of both apps.
- [.env.example](.env.example): every environment variable. `src/env.ts` checks them at start.

Run from the repository root:

```sh
pnpm --filter @repo/platform dev        # http://localhost:3001; needs the API (apps/api/docs/local-run.md)
pnpm --filter @repo/platform test       # Vitest: components, the BFF, the architecture rules
pnpm --filter @repo/platform test:e2e   # Playwright with Clerk's test mode and axe
pnpm --filter @repo/platform build
```
