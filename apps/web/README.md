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
