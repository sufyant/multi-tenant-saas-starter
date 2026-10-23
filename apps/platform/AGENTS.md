<!-- BEGIN:nextjs-agent-rules -->

## This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->

Read `docs/frontend/AGENTS.md` first. It holds the rules of both apps. This file adds only what differs for the platform app.

# The platform app

- The system admins use this app. On screen it is "Platform"; in code, routes and the API it is `platform` (N5). "Admin" is only a role of the tenant web.
- The routes have no tenant segment. Today the app has `/` (the Overview), `/settings/preferences`, `/two-step` and the sign-in routes `/sign-in` and `/sso-callback`. Its sections Organisations, Users and Activity come in later steps; a tenant is then a record the platform lists.
- An ink "Platform" label starts the top bar on every page and every screen size. There is no OrgMenu.
- A session without a verified second factor goes to `/two-step` first: the enrolment or the challenge (F42). Only then does the shell read `GetMe`.
- A signed-in user whose `GetMe.isSystemAdmin` is false sees "You don't have access". The first system admin has no user until their first platform request; on `GetMe` 404 the shell asks the door once (F42).
- The platform runs on its own origin, so the API accepts its tokens through `Host__Authentication__AuthorizedParties__1` (`apps/api/docs/configuration.md`, `.railway/railway.ts`).
- The dev server runs on port 3001, so both apps run side by side.
