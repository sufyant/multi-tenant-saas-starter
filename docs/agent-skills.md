# Agent skills

## 1. Purpose

An agent skill is a folder with a `SKILL.md` file. It gives a coding agent (Claude Code) the instructions for one tool, for example Clerk or Resend.

The skills of this repository live in `.claude/skills`. Each skill is a real folder, committed to the repository. The TanStack Table skills are the one exception: they ship inside their packages (section 3). A fresh clone, a cloud agent and CI get the same skills as the maintainer.

One exception: a cloud thread of Claude Code Projects does not load `.claude/skills` from the repository. The skills a cloud thread needs are uploaded to the maintainer's account. A new skill in the repository is also uploaded there (section 4).

`skills-lock.json` records the source, the path and the hash of each skill.

## 2. Rules

- Keep a skill when the repository uses its tool today or has a planned place for it (an app folder, a written plan). Remove it when neither is true.
- Never add a skill that contradicts a closed decision. Example: `clerk-orgs` is not here, because tenants, roles and permissions live in our tables, not in Clerk organizations (`apps/api/AGENTS.md`, closed decisions; rule T6).
- Never commit a symlink or an `.agents/` folder. A symlink points at a folder outside the repository, so a fresh clone gets nothing.
- Never edit the content of a skill. The next update replaces the whole folder, so a local change is lost.
- The license of a skill must be MIT, Apache-2.0, BSD or ISC. The repository is public, so it redistributes these files.
- A skill whose sample files fail dependency review is not committed. Dependency review in `ci.yml` reads every manifest in the repository, a sample `package.json` in a skill too.

Why so few skills: the name and the description of every skill load into every agent session. A skill for a tool we do not use or plan fills the context, and it can trigger on the wrong task (Claude Code docs, "Agent Skills": progressive disclosure).

CI checks the symlink rule. The step "Check the agent skills" in the `node` job of `ci.yml` runs `scripts/ci/check-agent-skills.sh`. It fails when `.claude/skills` holds a symlink, a folder without `SKILL.md`, or folders that differ from the skills in `skills-lock.json`.

## 3. The skills

| Skill | Source | License | Why we keep it |
| --- | --- | --- | --- |
| `clerk` | `clerk/skills` | MIT (declared, see below) | Routes a Clerk task to the right Clerk skill. |
| `clerk-setup` | `clerk/skills` | MIT (declared, see below) | Clerk setup in the web app. |
| `clerk-nextjs-patterns` | `clerk/skills` | MIT (declared, see below) | The web app uses Next.js with Clerk. |
| `clerk-backend-api` | `clerk/skills` | MIT (declared, see below) | The API calls the Clerk Backend API (`ClerkIdentityProvider`). |
| `clerk-testing` | `clerk/skills` | MIT (declared, see below) | The Playwright tests in `apps/web/e2e` run behind Clerk sign-in. |
| `clerk-expo` | `clerk/skills` | MIT (declared, see below) | `apps/mobile` is planned, and `.gitignore` names Expo. |
| `clerk-custom-ui` | `clerk/skills` | MIT (declared, see below) | The sign-in screens will carry the product's own look. |
| `clerk-cli` | `clerk/skills` | MIT (declared, see below) | Clerk instance setup from the terminal, for the production instance. |
| `resend` | `resend/resend-skills` | MIT (`LICENSE`) | The API sends email with Resend (`ResendEmailChannel`). |
| `resend-cli` | `resend/resend-skills` | MIT (`LICENSE`) | Domains, logs and test emails from the terminal. |
| `email-best-practices` | `resend/resend-skills` | MIT (`LICENSE`) | Deliverability, SPF, DKIM, DMARC and compliance. |
| `use-railway` | `railwayapp/railway-skills` | MIT (`LICENSE`) | The services and PostgreSQL run on Railway. |
| `shadcn` | `shadcn/ui` | MIT (`LICENSE.md`) | The web app uses shadcn/ui. |
| `turborepo` | `vercel/turborepo` | MIT (`LICENSE` at the repository root) | The monorepo runs its tasks with Turborepo (`turbo.json`). |
| TanStack Table skills | inside `@tanstack/react-table` and `@tanstack/table-core` | MIT (`license` in each `package.json`) | `packages/ui` builds the data table on TanStack Table v9. |

The TanStack Table skills are not in `.claude/skills`. They ship inside the packages, so updating the package updates them. `intent.skills` in the root `package.json` lists the enabled ones: `@tanstack/react-table#getting-started` and `#table-state`, `@tanstack/table-core#core`, `#custom-features`, `#table-features` and `#table-state`. The maintainer chose them one by one, never with "Enable all", because "Enable all" also enables the skills of every future package. `npx @tanstack/intent@0.5.6 install` writes that list and the `intent-skills` block at the top of `AGENTS.md`. The block tells an agent to run `pnpm exec intent list` and `pnpm exec intent load <package>#<skill>`, so `@tanstack/intent` is an exact root dev dependency (0.5.6, MIT, no dependencies). After adding another TanStack package, run `install` again.

The Clerk license: the repository `clerk/skills` has no `LICENSE` file. It declares MIT in its `README.md` ("License"), in `plugin.json` and in the frontmatter of each `SKILL.md` (`license: MIT`). The maintainer accepted this declaration in step 18j. If Clerk adds a `LICENSE` file, update this table.

One kept skill has a dependency manifest: `clerk-nextjs-patterns/templates/nextjs-basic-auth/package.json`. Its versions are `latest`, and it passes dependency review.

Not kept:

- `clerk-orgs`: tenants, roles and permissions live in our tables, not in Clerk organizations (`apps/api/AGENTS.md`, closed decisions; rule T6).
- `clerk-billing`: there is no billing (`apps/api/docs/ARCHITECTURE.md`, fixed rule 3).
- `clerk-android`, `clerk-swift`: the planned mobile app is Expo, not native.
- `clerk-astro-patterns`, `clerk-nuxt-patterns`, `clerk-vue-patterns`, `clerk-tanstack-patterns`, `clerk-react-router-patterns`, `clerk-chrome-extension-patterns`, `clerk-react-patterns`: no planned app uses these frameworks. The web, platform and marketing apps are Next.js, which `clerk-nextjs-patterns` covers. Their sample `package.json` files are manifests for dependency review. The one in `clerk-astro-patterns` failed it (`astro@^5.0.0`: one critical and two high advisories).
- `clerk-webhooks`: the API receives no webhook (`apps/api/docs/ARCHITECTURE.md`, rule A5).
- `react-email`: the API sends email from .NET, without React Email.
- `agent-email-inbox`: the API receives no email.
- `neon-postgres`: the database is Railway PostgreSQL, not Neon.
- `theme-factory`, `mcp-builder`: they serve no part of the product.
- `frontend-design` (`anthropics/skills`): it tells the agent to choose its own palette, typefaces and layout, but the design is fixed by `packages/ui/tokens/tokens.json`.
- `webapp-testing` (`anthropics/skills`): it writes Python Playwright scripts, but the repository tests the browser with TypeScript Playwright in `apps/<app>/e2e/` and `@clerk/testing` (`docs/frontend/ARCHITECTURE.md`, F35).
- `migrate-radix-to-base` (`shadcn/ui`): the components are on Base UI from the start.
- `react-best-practices` (`vercel-labs/agent-skills`): its rules recommend SWR for client data (`client-swr-dedup`), but the repository uses TanStack Query (closed decision, `docs/frontend/ARCHITECTURE.md`, F8).
- `next-best-practices` (`vercel-labs/next-skills`): Vercel retired it (README of `vercel-labs/next-skills`). The Next.js knowledge ships inside `next` (`node_modules/next/dist/docs/`) and in the block that `next dev` writes into `apps/web/AGENTS.md` and `apps/platform/AGENTS.md`.
- The two `migrate-v8-to-v9` skills of `@tanstack/react-table` and `@tanstack/table-core`: the repository started on TanStack Table v9.

A removed skill is one command away if a need appears (section 4).

## 4. Add, update or remove a skill

The list of skills lives once, in `skills-lock.json` (D1). No document or script repeats the list of skills. Agents never install, update or remove a skill. They report a stale or missing skill to the maintainer.

To add one skill, run the command from the repository root. `--copy` writes a real folder. `-a claude-code` installs for Claude Code only. Without `--copy`, the tool creates a store in `.agents/` and symlinks to it.

```bash
npx skills@1.7.2 add <source> --copy -a claude-code -y -s <skill>
```

Example, the skill `resend` from `resend/resend-skills`:

```bash
npx skills@1.7.2 add resend/resend-skills --copy -a claude-code -y -s resend
```

To update all of them, run `pnpm skills:update`. It runs `scripts/agent-skills/update.sh`, which reads each skill and its source from `skills-lock.json` and runs the command above for it. Then it removes `.agents/` and `agent/` and runs `scripts/ci/check-agent-skills.sh`. `npx skills update` is not used: skills@1.7.2 runs `skills add` without `--copy` for a changed skill, so it writes `.agents/` and a symlink.

The workflow `.github/workflows/agent-skills.yml` runs `pnpm skills:update` every Monday and on `workflow_dispatch`. It fails when a skill changed upstream and lists the changed skills in the job summary. It also fails when a skill can no longer be installed, for example after upstream removed or renamed it; then the log of the step `pnpm skills:update` names the skill, and the job summary is empty. It opens no pull request and no issue. The maintainer then runs `pnpm skills:update` on a branch and opens a pull request.

The TanStack Table skills update with their package (section 3).

After the command:

1. Check that `.agents/` and `agent/` do not exist. If they do, delete them. `.gitignore` excludes them.
2. Check the license of a new skill (section 2), and add a row to the table in section 3.
3. Run `scripts/ci/check-agent-skills.sh`.
4. Commit the folder, `skills-lock.json` and this document in one pull request.
5. The maintainer uploads a new skill to the account too, so a cloud thread of Claude Code Projects gets it (section 1).

To remove a skill, delete its folder and its entry in `skills-lock.json`, and remove its row from section 3.
