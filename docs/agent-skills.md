# Agent skills

## 1. Purpose

An agent skill is a folder with a `SKILL.md` file. It gives a coding agent (Claude Code) instructions that load only when a task needs them.

The repository holds two kinds:

- An upstream skill gives the instructions for one tool, for example Clerk or Resend. Its owner writes it in another repository, and we copy it here unchanged.
- An own skill gives a way of working of this repository that no document gives, for example how to build a screen from its Figma frames. We write and edit it here.

The skills of this repository live in `.claude/skills`. Each skill is a real folder, committed to the repository. The TanStack Table skills are the one exception: they ship inside their packages (section 3). A fresh clone, a cloud agent and CI get the same skills as the maintainer.

A cloud thread of Claude Code Projects is different: it does not load `.claude/skills` from the repository. The skills a cloud thread needs are uploaded to the maintainer's account. A new skill in the repository is also uploaded there (sections 4 and 5).

`skills-lock.json` records the source, the path and the hash of each upstream skill. An own skill is not in it.

## 2. Rules

- Keep a skill when the repository uses its tool today or has a planned place for it (an app folder, a written plan). Remove it when neither is true.
- Never add a skill that contradicts a closed decision. Example: `clerk-orgs` is not here, because tenants, roles and permissions live in our tables, not in Clerk organizations (`apps/api/AGENTS.md`, closed decisions; rule T6).
- Never commit a symlink or an `.agents/` folder. A symlink points at a folder outside the repository, so a fresh clone gets nothing.
- Never edit the content of an upstream skill. The next update replaces the whole folder, so a local change is lost.
- The license of a skill must be MIT, Apache-2.0, BSD or ISC. The repository is public, so it redistributes these files.
- A skill whose sample files fail dependency review is not committed. Dependency review in `ci.yml` reads every manifest in the repository, a sample `package.json` in a skill too.

Why so few skills: the name and the description of every skill load into every agent session. A skill for a tool we do not use or plan fills the context, and it can trigger on the wrong task (Claude Code docs, "Agent Skills": progressive disclosure).

An own skill carries a mark in the frontmatter of its `SKILL.md`:

```yaml
metadata:
  origin: repository
```

The mark tells the two kinds apart. We chose it from three options, because it keeps the list of skills in one place (D1) and changes the least:

- The mark in the frontmatter (chosen). The folder itself says what it is. `scripts/agent-skills/update.sh` reads only `skills-lock.json`, so it never touches an own skill, and `agent-skills.yml` needs no change. `metadata` is a field of the Agent Skills format, so the skill still uploads to the account.
- An entry in `skills-lock.json` with a local source. `update.sh` would try to install it from upstream, and skills@1.7.2 has no local source type.
- A second list of own skills, in a file or in the script. It is a second list to keep in step with the folders.

CI checks the rules. The step "Check the agent skills" in the `node` job of `ci.yml` runs `scripts/ci/check-agent-skills.sh`. It fails when `.claude/skills` holds a symlink, a folder without `SKILL.md`, or upstream folders that differ from the skills in `skills-lock.json`. An upstream folder is a folder without the mark.

## 3. The skills

The upstream skills:

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

Intent reads the skills from the installed packages, so the check needs a full `pnpm install` from the workspace root. `intent list` scans the `node_modules` folder of each workspace package, and `@tanstack/react-table` is a dependency of `packages/ui`, not of the root. Without an install, a worktree gives one of two answers:

- No `node_modules` at all: `pnpm exec intent list` fails with "Command intent not found".
- Only the root `node_modules` (for example a copy from the main checkout): Intent runs, but it answers "No intent-enabled packages found", and each skill of `intent.skills` is "declared but was not discovered".

After `pnpm install`, `intent list` names the two packages and the six skills. It also notes one hidden source with two skills. The only skills of the two packages outside `intent.skills` are the two `migrate-v8-to-v9` skills (see "Not kept" below). `intent list --show-hidden` names the source, and Intent asks the maintainer, not an agent, to run it. The text between `<!-- intent-skills:start -->` and `<!-- intent-skills:end -->` in `AGENTS.md` belongs to `intent install`, so the install rule stands after that block.

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

The own skills:

| Skill | Why we keep it |
| --- | --- |
| `building-screens-from-design` | Builds a screen from its Figma frames: every value has a source, and each frame gets a screenshot that is compared in the same pull request. |
| `starting-roadmap-steps` | Prepares a roadmap step: every frame has one pull request, every value an API field, every question is asked first. |

Not written, each for its evaluation (`docs/agent-skill-evaluations.md`): writing tests, adding an endpoint, opening and watching a pull request, and reading logs. Without a skill, the agent already did what the documents ask. The rules that no document held went into `AGENTS.md`: the rebase, the heading "Maintainer's tasks", the Railway variables and the deployed database.

## 4. Add, update or remove an upstream skill

The list of upstream skills lives once, in `skills-lock.json` (D1). No document or script repeats it. Agents never install, update or remove an upstream skill. They report a stale or missing skill to the maintainer.

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

## 5. Write or change an own skill

An own skill is written only for a way of working that no document gives. A skill that repeats `AGENTS.md` or an `ARCHITECTURE.md` costs context in every session: its name and description load into each one (section 2).

Follow Anthropic's "Skill authoring best practices". In this repository:

- The evaluation comes first. Run its scenario once without the skill, with a subagent, and write what the agent did. Write the skill. Run the scenario again with the skill. Keep the skill only if it changed the result. A change to a skill runs its evaluation again. The scenarios and the results are in `docs/agent-skill-evaluations.md`.
- A skill points to a rule in its document. It never copies the rule (D1).
- The freedom fits the risk. Exact instructions only where a mistake is costly; elsewhere the usual path and when to stop. A skill does not forbid what the documents allow.
- The name is a lowercase gerund with hyphens (`building-screens-from-design`). The description is in the third person and says what the skill does and when to use it.
- A long workflow has a checklist that the agent copies into its work.
- Nothing that goes stale: no pull request numbers, no dates, no stories of past mistakes.
- `SKILL.md` stays under 500 lines. A longer part goes into a file that `SKILL.md` links to directly.
- The frontmatter carries the mark of section 2.

After the merge, the maintainer uploads a new or changed own skill to the account, so a cloud thread gets it (section 1).
