# AGENTS.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- A file's name and folder follow N8.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

**End every report with what you skipped, what you did not check, and the risks.** A report is a pull request description, a review, or the answer to a task. Write "none" when there is nothing. The reader must not find a gap later that you knew about.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

## Repository settings

- GitHub settings are code. They live in `docs/github-settings.md`, `.github/rulesets/` and `scripts/github/apply-settings.sh`.
- Never change a setting only in the GitHub interface. Change the document and the script or the JSON in a pull request. The maintainer then runs `pnpm github:apply`.
- Never run `pnpm github:apply` yourself. You may run `pnpm github:check`.
- Never turn on auto-merge for a pull request. The maintainer does it.
- Railway settings are code: `.railway/railway.ts`. Never run `pnpm railway:apply` yourself. You may run `pnpm railway:plan`.
- Clerk settings are code: `.clerk/`. Never run `pnpm clerk:apply` yourself. You may run `pnpm clerk:plan`.
- Never change a user or a setting in the Clerk dashboard.
- Never create or push a `v*` tag. A release is the maintainer's decision.
- Never approve a production deployment. It is the maintainer's decision.

## Workflows

- `ci.yml` verifies. `delivery.yml` delivers. `rollback.yml` deploys an earlier release to production. `deploy.yml` holds the only copy of the deploy steps.
- A `run` block over 15 lines goes into `scripts/ci/` or `scripts/deploy/`.

## Agent skills

- Skills live in `.claude/skills` as real folders. Never commit a symlink or an `.agents/` folder.
- Add a skill only for a tool this repository uses or has a planned place for. `docs/agent-skills.md` lists them.

## Monorepo tasks

- In `turbo.json`, the web `build` depends on its own `check-types`, because `next build` cleans `.next` while `next typegen` writes to it.
