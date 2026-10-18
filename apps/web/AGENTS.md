<!-- BEGIN:nextjs-agent-rules -->

# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->

Read `docs/frontend/AGENTS.md` first. It holds the rules of both apps. This file adds only what differs for the tenant web.

# The tenant web

- The members of an organisation use this app. On screen a tenant is an "organisation".
- Every signed-in route sits under `/[tenantSlug]/...` (F30). The tenant layout turns the slug into the tenant id through the cached `ListMyTenants` (`src/shared/tenant-context.tsx`: `TenantProvider`, `useTenant`, `useCan`), and every call of a tenant operation takes that id. A slug that is not in the user's list shows "No access yet".
- `/` is "Choose an organisation". With one organisation it redirects to `/[tenantSlug]`.
- The breadcrumb starts with the organisation name. It opens the OrgMenu when the user has more than one organisation.
- What the user may do comes from `GetMyTenant.permissions` (F5).
