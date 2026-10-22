# GitHub settings

## 1. Purpose

These settings apply to this repository. They also apply to every repository that you create from this template.

GitHub does not copy repository settings to a new repository from a template. Thus each product repository needs the same settings again.

The script `scripts/github/apply-settings.sh` is the source of truth. The rulesets are in `.github/rulesets/`.

The Railway settings are code too, in `.railway/railway.ts`. `docs/deployment.md` describes them.

To change a setting, open a pull request. The pull request changes this document and the script or the JSON. After the merge, run `pnpm github:apply`.

Do not change a setting only in the GitHub interface. A change that is only in the interface is configuration drift. The next run of `pnpm github:apply` removes it.

Source: Kief Morris, *Infrastructure as Code* (2nd ed.), chapter 2: "Define everything as code" and "Configuration drift".

## 2. How to apply

You must be an admin of the repository. Run the commands from the repository root.

1. Log in with the GitHub CLI:

   ```bash
   gh auth login
   ```

2. Apply the settings to a product repository:

   ```bash
   pnpm github:apply
   ```

3. Check the settings. This command changes nothing:

   ```bash
   pnpm github:check
   ```

Without a repository name, the script uses the repository of the current folder. To use a different repository, give its name. For example: `pnpm github:check -- <owner/repo>`. The script prints the repository name before it reads or changes anything.

The two commands run `scripts/github/apply-settings.sh`. Run `pnpm github:check -- --help` to see all options.

The script prints one line per setting:

- `OK`: the setting has the expected value.
- `DIFF expected=... actual=...`: the setting has a different value.
- `FAIL`: the script could not read or write the setting. The line gives the reason.

The script exits with 1 if one line is `DIFF` or `FAIL`. Without `--check`, the script applies the settings first and then checks them. A second run changes nothing.

The script uses only `gh api`. It needs no other tool.

## 3. Settings

### 3.1 General

The script sets these values with `PATCH /repos/{owner}/{repo}`.

| Setting | Value | Reason | Source |
| --- | --- | --- | --- |
| `has_wiki` | `false` | Documents live in the repository, in `docs/` and in `apps/*/docs/`. A wiki is a second place that nobody reviews. | GitHub Docs: "Disabling wikis" |
| `has_projects` | `false` | The repository does not use GitHub Projects. | GitHub Docs: "Disabling projects in a repository" |
| `has_discussions` | `false` | Issues are the only place for questions and reports. The REST API cannot set this value. See section 5. | GitHub Docs: "Enabling or disabling GitHub Discussions for a repository" |
| `has_issues` | `true` | Issues record bugs and planned work. | GitHub Docs: "About issues" |
| `allow_merge_commit` | `true` | `apps/api/AGENTS.md` asks for small commits in order: test, change, documents. A merge commit keeps that order in the history of `main`. | GitHub Docs: "About merge methods on GitHub" |
| `allow_squash_merge` | `false` | A squash merge removes the order of the commits. | GitHub Docs: "About merge methods on GitHub" |
| `allow_rebase_merge` | `false` | A rebase merge removes the merge commit that marks the pull request. | GitHub Docs: "About merge methods on GitHub" |
| `allow_auto_merge` | `true` | The maintainer turns auto-merge on for each pull request he approves. GitHub merges it when the required checks pass and the branch is up to date. Agents never turn it on. | GitHub Docs: "Managing auto-merge for pull requests in your repository". GitHub Docs: "Automatically merging a pull request" |
| `allow_update_branch` | `true` | The pull request page shows a button that updates the branch from `main`. | GitHub Docs: "Managing suggestions to update pull request branches" |
| `delete_branch_on_merge` | `true` | Merged branches do not stay in the repository. | GitHub Docs: "Managing the automatic deletion of branches" |

### 3.2 Security

| Setting | Value | Reason | Source |
| --- | --- | --- | --- |
| Secret scanning | enabled | GitHub finds secrets that are in the repository. | GitHub Docs: "About secret scanning" |
| Secret scanning push protection | enabled | GitHub blocks a push that contains a secret. | GitHub Docs: "About push protection" |
| Dependabot alerts | enabled | GitHub tells you when a dependency has a known vulnerability. | GitHub Docs: "About Dependabot alerts". OpenSSF Scorecard: Vulnerabilities |
| Dependabot security updates | enabled | Dependabot opens a pull request that fixes a vulnerable dependency. | GitHub Docs: "About Dependabot security updates". OpenSSF Scorecard: Vulnerabilities |
| Private vulnerability reporting | enabled | A researcher can report a vulnerability privately. The report is not a public issue. | GitHub Docs: "Configuring private vulnerability reporting for a repository" |
| CodeQL code scanning | default setup, state `configured` | CodeQL finds security problems in the code on each push and pull request. | GitHub Docs: "Configuring default setup for code scanning". OpenSSF Scorecard: SAST |
| Security policy | `SECURITY.md` at the repository root | It tells a researcher to use private vulnerability reporting and not a public issue. | GitHub Docs: "Adding a security policy to your repository". OpenSSF Scorecard: Security-Policy |
| Dependabot version updates | `.github/dependabot.yml`: NuGet, npm (for pnpm) and GitHub Actions, weekly, minor and patch updates grouped per ecosystem, security updates grouped, cooldown 7 days. Docker base images daily with no cooldown, because the image scan fails on a vulnerability with a fix. Node major versions are ignored for the Docker images; a Node major upgrade is planned | Dependencies stay current in few pull requests. A new release waits 7 days, because a compromised release is often pulled within days. | GitHub Docs: "Dependabot options reference". OpenSSF Scorecard: Dependency-Update-Tool |
| Dependency review | Job `dependency-review` in `.github/workflows/ci.yml`, on pull requests only. `.github/dependency-review-config.yml` lists the allowed licenses, fails on a vulnerability of high severity or more, and posts no comment | Each dependency that a pull request adds or changes is checked for its license and its known vulnerabilities, in every ecosystem. Before, only the NuGet packages had a license check (`LicenseTests`). The check needs the dependency graph, which is free on public repositories. A private repository needs GitHub Code Security | GitHub Docs: "About dependency review". `actions/dependency-review-action` README, "Configuration options" |

A Dependabot pull request that updates a generator package, such as Wolverine or Orval, can fail the job "Generated files: current" in `ci.yml`, because the new version writes other code ("Generated files" in section 2 of `apps/api/docs/ARCHITECTURE.md`). Fix it on that pull request: check out its branch, run `pnpm generate` from the repository root, commit what it changes, and push. After a push of another author, Dependabot no longer rebases the pull request by itself (GitHub Docs: "Managing pull requests for dependency updates").

### 3.3 Actions

| Setting | Value | Reason | Source |
| --- | --- | --- | --- |
| Actions enabled | `true` | The CI step adds workflows. | GitHub Docs: "Managing GitHub Actions settings for a repository" |
| Allowed actions | selected: actions created by GitHub, and Marketplace actions by verified creators. No other patterns. | An unknown action runs code with access to the repository. | GitHub Docs: "Managing GitHub Actions settings for a repository" |
| SHA pinning required | `true` | A tag can move to different code. A full-length commit SHA cannot. | GitHub Docs: "Secure use reference" (pin actions to a full-length commit SHA). OpenSSF Scorecard: Pinned-Dependencies |
| Default workflow permissions | `read` | The `GITHUB_TOKEN` gets the least permission. A workflow asks for more permission only where it needs it. | GitHub Docs: "Controlling permissions for GITHUB_TOKEN". OpenSSF Scorecard: Token-Permissions |
| Actions can create and approve pull requests | `false` | A workflow must not approve its own change. | GitHub Docs: "Managing GitHub Actions settings for a repository" |
| Fork pull request approval | all external contributors | A maintainer approves each workflow run from a fork before it runs. | GitHub Docs: "Approving workflow runs from forks" |

### 3.4 Ruleset for the default branch

The file `.github/rulesets/main.json` holds the ruleset. It uses the format that GitHub uses to import and export rulesets. The script finds the repository ruleset named `main`. If it exists, the script updates it. If not, the script creates it.

| Rule | Value | Reason | Source |
| --- | --- | --- | --- |
| Target | the default branch (`~DEFAULT_BRANCH`) | The rules follow the default branch if its name changes. | GitHub Docs: "Creating rulesets for a repository" |
| Bypass actors | none | Nobody can skip the rules, including admins. | GitHub Docs: "About rulesets". OpenSSF Scorecard: Branch-Protection |
| `deletion` | on | Nobody can delete `main`. | GitHub Docs: "Available rules for rulesets". OpenSSF Scorecard: Branch-Protection |
| `non_fast_forward` | on | Nobody can force push to `main`. | GitHub Docs: "Available rules for rulesets". OpenSSF Scorecard: Branch-Protection |
| `pull_request` | on | Every change to `main` goes through a pull request. | GitHub Docs: "Available rules for rulesets". OpenSSF Scorecard: Branch-Protection |
| Required approving reviews | `0` | GitHub does not let a single maintainer approve their own pull request. | GitHub Docs: "About pull request reviews" |
| Dismiss stale reviews, code owner review, last push approval | `false` | These rules need a second reviewer. The project has one maintainer. | GitHub Docs: "Available rules for rulesets" |
| Required review thread resolution | `true` | A pull request cannot merge while a review comment is open. | GitHub Docs: "Available rules for rulesets" |
| Allowed merge methods | `merge` | This matches the general settings in section 3.1. | GitHub Docs: "Available rules for rulesets" |
| Required status checks | `CI` from GitHub Actions (`integration_id` 15368) | `CI` is the last job of `.github/workflows/ci.yml`, which only verifies. It fails when a job in its `needs` list did not pass. A new job is added to `needs` in the workflow, not here. `delivery.yml` calls the same workflow on a push to `main`. | GitHub Docs: "Available rules for rulesets". Humble and Farley, *Continuous Delivery*, chapter 3 |
| Require branches to be up to date (`strict_required_status_checks_policy`) | `true` | CI tests the code that will be on `main` after the merge, not an old base. | GitHub Docs: "Available rules for rulesets" |

With `strict_required_status_checks_policy`, a branch that falls behind `main` waits. Auto-merge does not update it. Update the branch (button or rebase), and auto-merge continues after the checks pass again. Source: GitHub Docs: "Automatically merging a pull request".

### 3.5 Ruleset for release tags

The file `.github/rulesets/release-tags.json` holds the ruleset, in the same format as `main.json`. The script finds the repository ruleset named `release-tags`, and updates or creates it like `main`.

A release tag `vYYYY.0M.0D.N` names a version that ran in production. The job `tag-release` in `.github/workflows/delivery.yml` creates it with `GITHUB_TOKEN` after a successful production deploy (`docs/deployment.md`, section "Versions"). The ruleset does not restrict creation ("Restrict creations" is off), so the job can create the tag without a bypass. `deletion` and `update` stop a delete and a push to an existing tag. A new tag is neither.

| Rule | Value | Reason | Source |
| --- | --- | --- | --- |
| Target | tags, include `refs/tags/v*` | The rules apply to release tags only. | GitHub Docs: "Creating rulesets for a repository" |
| Bypass actors | none | Nobody can skip the rules, including admins. This matches section 3.4. | GitHub Docs: "About rulesets" |
| `deletion` | on | Nobody can delete a release tag. A release stays traceable to its commit. | GitHub Docs: "Available rules for rulesets" |
| `update` | on | Nobody can move a release tag to another commit. A version always names the same commit, and so the same image. | GitHub Docs: "Available rules for rulesets", "Restrict updates" |

### 3.6 Environments `staging` and `production`

`.github/workflows/deploy.yml` runs in one of these environments. On a push to `main`, `delivery.yml` calls it for `staging`, and then for `production` after the maintainer's approval. `rollback.yml` calls it for `production` on a release tag. The script creates each environment with `PUT /repos/{owner}/{repo}/environments/{name}` and sets its deployment branches and tags with `/deployment-branch-policies`.

| Setting | Value | Reason | Source |
| --- | --- | --- | --- |
| Deployment branches and tags of `staging` | custom policy: the branch `main` only. The script removes any other policy | Only a run on `main` can read the staging secrets and deploy. A run on a pull request branch cannot. | GitHub Docs: "Managing environments for deployment", "Deployment branches and tags" |
| Deployment branches and tags of `production` | custom policy: the branch `main` (the CI run) and the tag pattern `v*` (the rollback). The script removes any other policy | Only a run on `main` or on a release tag can deploy to production. A run on a pull request branch cannot. | GitHub Docs: "Managing environments for deployment", "Deployment branches and tags" |
| Required reviewers of `staging` | none | Merging the pull request is the approval. | GitHub Docs: "Managing environments for deployment", "Required reviewers" |
| Required reviewers of `production` | the repository owner, when the owner is a user. The script reads the owner's id with `gh api users/<owner>`. For an organisation owner, the script prints `FAIL`: set a team as the reviewer by hand, in the environment's settings | The approval is the release decision ("push-button release", Humble and Farley, *Continuous Delivery*, chapter 5). Every production job, also a rollback, waits for it. | GitHub Docs: "Managing environments for deployment", "Required reviewers" |
| `prevent_self_review` | `false` | The project has one maintainer, who approves their own deploys. `true` would block every deploy. | GitHub Docs: "Deployments and environments", "Required reviewers" |
| Wait timer | `0`, on both | The approval is the only wait. | GitHub Docs: "Managing environments for deployment", "Wait timer" |
| `can_admins_bypass` | `false`, on both | The setting lets an admin start a job that waits for a deployment protection rule, without the rule ("Start all waiting jobs"). With `false`, nobody skips the production approval. It matches section 3.4, which allows no bypass. | GitHub Docs: "Deployments and environments", "Allow administrators to bypass configured protection rules". GitHub Docs: "Reviewing deployments", "Bypassing deployment protection rules" |

Each environment holds three secrets and one variable. Each value is different per environment. The script never sets or reads them.

| Name | Kind | What it holds | Who sets it |
| --- | --- | --- | --- |
| `RAILWAY_TOKEN` | secret | A Railway project token for the Railway environment of the same name | The maintainer, with `gh secret set RAILWAY_TOKEN --env <environment>` |
| `MIGRATIONS_CONNECTION_STRING` | secret | The owner role's direct connection to that environment's database, for the `migrate` command only (R3 in `apps/api/docs/ARCHITECTURE.md`) | The maintainer, with `gh secret set MIGRATIONS_CONNECTION_STRING --env <environment>` |
| `MIGRATIONS_ROOT_CERTIFICATE` | secret | The root certificate of that environment's database server. The `migrate` command verifies the server's certificate against it. The certificate is not confidential | The maintainer, with `gh secret set MIGRATIONS_ROOT_CERTIFICATE --env <environment> < <file>` |
| `API_URL` | variable | The public URL of `api-web` in that environment. The smoke test of `deploy.yml` calls it | The maintainer, with `gh variable set API_URL --env <environment> --body <URL>` |

A caller passes the three secrets to `deploy.yml` by name. The called job sets `environment`, so it gets that environment's values (GitHub Docs: "Reuse workflows", "Using inputs and secrets in a reusable workflow").

`docs/deployment.md` gives the order of the first deployment and the format of each value.

### 3.7 Repository secrets for the Playwright tests

The job `e2e` in `.github/workflows/ci.yml` runs the Playwright tests of `apps/web` and `apps/platform` (F35 and F37 in `docs/frontend/ARCHITECTURE.md`). The tests sign in through a Clerk development instance, so the job needs three repository secrets. The script never sets or reads them.

| Name | Kind | What it holds | Who sets it |
| --- | --- | --- | --- |
| `CLERK_PUBLISHABLE_KEY_TEST` | secret | The publishable key (`pk_test_...`) of the Clerk development instance. It is not confidential, but the job reads the three values from one place | The maintainer, with `gh secret set CLERK_PUBLISHABLE_KEY_TEST` |
| `CLERK_SECRET_KEY_TEST` | secret | The secret key (`sk_test_...`) of the same instance. Never a key of the production instance | The maintainer, with `gh secret set CLERK_SECRET_KEY_TEST` |
| `E2E_CLERK_USER_EMAIL` | secret | The address of a user of the same instance, with `+clerk_test` in it. It signs in with the code `424242`, and no email goes out | The maintainer, with `gh secret set E2E_CLERK_USER_EMAIL` |

GitHub gives no repository secret to a workflow run from a fork's pull request, and gives a Dependabot pull request the Dependabot secrets only (GitHub Docs: "Using secrets in GitHub Actions", "Automating Dependabot with GitHub Actions"). `delivery.yml` passes no secret to `ci.yml`. In these runs the job skips its steps with the notice "E2E skipped" and passes. To run the tests on Dependabot pull requests too, set the same three values with `gh secret set <name> --app dependabot`.

## 4. Plan limits

Rulesets, including tag rulesets, and environment secrets are enforced on private repositories only with GitHub Pro, Team or Enterprise. The same is true for environments and their deployment branches and tags.

Required reviewers work on public repositories only, on GitHub Free, Pro and Team. On a private product repository on these plans, the script prints `FAIL` for the production reviewer, and production would deploy without an approval. Do not set `DEPLOY_PRODUCTION` there.

Artifact attestations work on public repositories on every current plan. A private repository needs GitHub Enterprise Cloud. `publish-image.yml` then fails at its attestation step.

On GitHub Free, they work only on public repositories.

A private product repository on GitHub Free is not protected. The script prints `FAIL` for each setting that the plan does not allow. It continues with the other settings and exits with 1.

Source: GitHub Docs: "About rulesets", "Managing environments for deployment", "Deployments and environments" ("Required reviewers"), "Using artifact attestations to establish provenance for builds" and "GitHub's plans".

## 5. Manual steps

The script cannot apply these settings. Do them by hand and check them again after each change.

### 5.1 Account

- Turn on two-factor authentication.
- Add a passkey.
- Store the recovery codes in a safe place.
- Turn on "Keep my email addresses private".
- Turn on "Block command line pushes that expose my email".
- Set the commit email to the GitHub noreply address. Do this on every machine and in the Claude Code cloud environment setup script.
- Review the authorised OAuth apps, GitHub Apps and personal access tokens. Use fine-grained tokens only. Give each token one repository, the least permission and an expiry date.

Source: GitHub Docs: "Configuring two-factor authentication", "About passkeys", "Setting your commit email address", "Reviewing your authorized integrations", "Managing your personal access tokens".

### 5.2 Repository

- Collaborators: the list is empty.
- Deploy keys: the list is empty.
- Webhooks: the list is empty.
- Installed GitHub Apps: each app has only the permissions it needs.
- The About description and the topics are set.
- Template repository: set only in the template repository, once. Settings, General, "Template repository". A repository created from the template is not a template by default, so a product repository needs no action.

### 5.3 Settings without a REST API endpoint

- `has_discussions` = `false`. The `PATCH /repos/{owner}/{repo}` endpoint does not accept `has_discussions`. Go to Settings, General, Features, and clear "Discussions". The script still reads the value and prints `OK` or `DIFF`.

### 5.4 Deployment

Do these steps in the order of `docs/deployment.md`, section "First deployment".

- Make each GHCR package public after its first push: `api`, `web` and `platform`. A new package is private, also when it comes from a public repository. A package that is linked to its repository gets the repository's access permissions, but not its visibility. Railway pulls the image without credentials, so the package must be public. Go to the package's settings, "Danger Zone", "Change visibility", and choose "Public". You cannot make a public package private again.
- For each environment, set its variables `API_URL`, `WEB_URL` and `PLATFORM_URL` to the public URLs of `api-web`, `web` and `platform` in that environment: `gh variable set API_URL --env <environment> --body <URL>`, and the same for the other two. Without them, the smoke tests of `deploy.yml` fail. `docs/deployment.md`, section 13, gives the domains.
- Set the repository variable `DEPLOY_STAGING` to `true` with `gh variable set DEPLOY_STAGING --body true`, after staging is set up. Without it, the job `deploy-staging` is skipped and `main` stays green. To stop the staging deploys, delete the variable with `gh variable delete DEPLOY_STAGING`.
- Set the repository variable `DEPLOY_PRODUCTION` to `true` with `gh variable set DEPLOY_PRODUCTION --body true`, after production is set up and when the product has users. Without it, the jobs `deploy-production` and `tag-release` are skipped and `main` stays green. To stop the production deploys, delete the variable with `gh variable delete DEPLOY_PRODUCTION`.

Source: GitHub Docs: "Configuring a package's access control and visibility" and "Store information in variables".

## 6. Planned changes

| What | When | Why not now |
| --- | --- | --- |
| `LICENSE` | After the maintainer decides the licence | The maintainer has not decided the licence. |
| Required signed commits | After commit signing works for cloud agents | Cloud agents cannot sign commits yet. The rule would block their pull requests. |
| Keep the Grype, Syft and Railway CLI image digests in `ci.yml`, `delivery.yml` and `deploy.yml`, and the PostgreSQL image digest in `apps/api/db/postgres-image`, current. Proposal: write the images as `FROM` lines in a file that Dependabot's `docker` ecosystem watches, for example `.github/images/Dockerfile`. The jobs read the references from that file. Dependabot then opens the pull requests | A later CI step | Dependabot's `github-actions` ecosystem does not update an image in a `run` step. Until then, the maintainer updates the digests by hand. |
| NuGet lock files (`packages.lock.json`) and restore in locked mode | Later; check first that Dependabot updates them | A lock file that Dependabot does not update would block its pull requests. `setup-dotnet` caches NuGet packages only with lock files. |
| Upgrade Node to the next LTS line (`.nvmrc`, the build image and the distroless runtime image together) | Before Node 22 reaches end of life (April 2027) | A major upgrade needs its own tested pull request. |
| Required code scanning results in the ruleset | After CodeQL has run on a few pull requests | A new rule must not block pull requests before the CodeQL results are known. |
| Plan `.railway/railway.ts` on pull requests and apply it on merge | When the plan output is known to be stable | The maintainer applies by hand until then. |
| Set up the `production` environment (`docs/deployment.md`, section 4) and set `DEPLOY_PRODUCTION` | In the product repository, before its first real user | The template has no users. A product that starts from the template in a private repository uses a different approval (`docs/deployment.md`, section 12). |
| Remove the Dependabot advisories of `uuid`, `stream-json` and `braces` | When a fixed version fits the parents' ranges | `docs/deployment.md`, section 10. |
