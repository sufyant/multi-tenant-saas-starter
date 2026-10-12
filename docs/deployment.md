# Deployment

This document describes how the API goes from `main` to staging, and after the maintainer's approval to production, on Railway. The web app goes to Vercel in a later step.

## 1. Purpose and sources

The pipeline builds the API image once, tests it, scans it and publishes that same image. Each environment runs that image by digest. Railway builds nothing, and the repository is not connected to Railway.

Sources:

- Humble and Farley, *Continuous Delivery*, chapter 5: "Only build your binaries once", "Deploy the same way to every environment", "Smoke-test your deployments", "Deploy into a copy of production", and the push-button release.
- The Twelve-Factor App, factor V ("Build, release, run") and factor III ("Config").
- SLSA v1.0, Build Level 2: provenance from a hosted build platform.
- Kief Morris, *Infrastructure as Code* (2nd ed.), chapter 2.
- Calendar Versioning (calver.org).

## 2. The flow

| File | Trigger | Jobs |
| --- | --- | --- |
| `ci.yml` | `pull_request`, and `workflow_call` from `delivery.yml` | Verification only: `node`, `api`, `api-image`, `web-image`, `dependency-review`, and the aggregator `CI`. No publish, no deploy. |
| `delivery.yml` | `push` to `main` | Calls `ci.yml`, then `publish-api`, `deploy-staging`, `deploy-production`, `tag-release`, in this order |
| `deploy.yml` | `workflow_call` | The only copy of the deploy steps, for staging and production |
| `rollback.yml` | `workflow_dispatch` on a release tag | No build. Deploys the image of that release to **production**, after the approval |

Which file to open:

| Question | File |
| --- | --- |
| Why did a pull request fail? What does CI check? | `.github/workflows/ci.yml`, and the scripts in `scripts/ci/` |
| What happens after a merge? In which order? | `.github/workflows/delivery.yml` |
| How does a deploy work, in staging or production? | `.github/workflows/deploy.yml`, and the scripts in `scripts/deploy/` |
| How do I roll production back? | `.github/workflows/rollback.yml`, and section 6 |
| Which GitHub settings apply? | `docs/github-settings.md` and `scripts/github/apply-settings.sh` |
| Which Railway settings apply? | `.railway/railway.ts` and section 7 |

A workflow calls a script for each long step. A script reads its inputs from environment variables, and its header says how to run it locally when it needs no secret (Humble and Farley, *Continuous Delivery*, chapter 6, "Build and Deployment Scripting").

On a push to `main`, after `ci.yml` passed (the jobs `node`, `api`, `api-image`, `web-image` and `CI`):

1. `publish-api` pushes the tested image to GHCR as `ghcr.io/<owner>/<repo>/api`, with the tags `<commit SHA>` and `main`. It reads the digest from the registry. It attests the build provenance and an SPDX SBOM to that digest, and verifies both attestations.
2. `deploy-staging` calls `deploy.yml` for `staging`, when the repository variable `DEPLOY_STAGING` is `true`.
3. `deploy-production` waits for the maintainer's approval, then calls `deploy.yml` for `production` with the same digest. It runs only when `deploy-staging` passed and the repository variable `DEPLOY_PRODUCTION` is `true`.
4. `tag-release` runs only when `deploy-production` passed. It creates the release tag on the commit and on the image digest in GHCR (section 5).

`deploy.yml` holds the only copy of the deploy steps. In the GitHub environment that it gets:

1. It checks that the two secrets are set.
2. For `production`, it checks that the commit is not older than the commit that production runs (section 6). A rollback skips this check.
3. It runs `migrate` with the image digest.
4. It sets the digest as the image of `api-web` and `api-worker` in one Railway commit.
5. It waits until the new deployment of each service is `SUCCESS`.
6. It smoke-tests the public route: `GET /v1/me/tenants` without a token must answer 401, and `GET /health/ready` must answer 404. The 401 shows that the route reaches the web process and its authentication. The 404 shows that the public route does not reach the health endpoints: they answer only on the management port, 8081. Railway's health check covers readiness there.

### Approve a production deploy

The approval is the release decision. Only the maintainer approves.

1. Open the repository on GitHub, then Actions, then the run of the commit.
2. Click "Review deployments".
3. Select `production` and click "Approve and deploy". "Reject" ends the run without a deploy.

`gh` has no command for this (cli/cli issue 5773, open). The REST endpoint `POST /repos/{owner}/{repo}/actions/runs/{run_id}/pending_deployments` exists, but this document uses the web page. Source: GitHub Docs, "Reviewing deployments".

### Runs that wait

Only one deploy to an environment runs at a time (`concurrency` in `deploy.yml`). GitHub never cancels a running deploy. Each push run on `main` has a concurrency group of its own, so a newer push tests and deploys to staging while an older run waits for its approval.

A production job that waits for its approval holds the group `deploy-production`. GitHub does not cancel it when a newer run arrives. The newer run's production job is `pending` behind it, and a still newer one replaces that pending job. So:

- Approve or reject the waiting run before the next production deploy can wait for its own approval.
- To deploy the newest commit, reject the older waiting run. The newest pending run then waits for its approval.

Sources: GitHub Docs, "Control the concurrency of workflows and jobs" (one running and one pending job per group). GitHub community discussion 135220 (a waiting run holds the group; GitHub staff confirmed it in August 2024). The GitHub docs do not describe this case.

If the maintainer approves an old run after a newer commit reached production, the check in `deploy.yml` stops the deploy before the migration. The message says to approve the newest run, or to use the rollback.

On a pull request, only `ci.yml` runs. No publish, deploy or tag job runs.

## 3. What lives where

The table holds names only. A value never goes into the repository.

| Setting | Place | `staging` | `production` |
| --- | --- | --- | --- |
| The workflows, the image name, the Syft and Railway CLI image digests | Repository: `.github/workflows/` | same | same |
| The services `api-web` and `api-worker`, their region, replicas and health check | Repository: `.railway/railway.ts` | same | same |
| `Host__Role`, `ASPNETCORE_ENVIRONMENT`, `PORT` | Repository: `.railway/railway.ts`, as literal values | same | same |
| The names of the shared variables that each service uses | Repository: `.railway/railway.ts` | same | same |
| The environments, their reviewers, their deployment branches and tags, the ruleset for release tags | Repository: `scripts/github/apply-settings.sh`, `.github/rulesets/` and `docs/github-settings.md` | branch `main`, no reviewer | branch `main` and tags `v*`, the owner as reviewer |
| `RAILWAY_TOKEN` | GitHub environment secret | the staging project token | the production project token |
| `MIGRATIONS_CONNECTION_STRING` | GitHub environment secret | the staging database | the production database |
| `API_URL` | GitHub environment variable | the staging URL of `api-web` | the production URL of `api-web` |
| `DEPLOY_STAGING`, `DEPLOY_PRODUCTION` | GitHub repository variables | `DEPLOY_STAGING=true` switches the staging deploy on | `DEPLOY_PRODUCTION=true` switches the production deploy and the release tag on |
| `ConnectionStrings__Database` | Railway shared variable, sealed | the staging database | the production database |
| `ConnectionStrings__Messaging` | Railway shared variable, sealed | the staging database | the production database |
| `Authentication__Clerk__Issuer` | Railway shared variable | own value | own value |
| `Authentication__Clerk__AuthorizedParties__0` | Railway shared variable | own value | own value |
| `ControlPlane__FirstSystemAdminEmail` | Railway shared variable | own value | own value |
| `ControlPlane__Clerk__SecretKey` | Railway shared variable, sealed | own value | own value |
| `ControlPlane__Invitations__AcceptUrl` | Railway shared variable | own value | own value |
| `Notifications__Resend__ApiKey` | Railway shared variable, sealed | own value | own value |
| `Notifications__Resend__From` | Railway shared variable | own value | own value |
| `Host__Cors__AllowedOrigins__0` | Railway shared variable | own value | own value |
| The image digest that each service runs | Railway service settings. `deploy.yml` sets it | the latest commit on `main` | the latest approved commit, or a rolled back release |
| The generated domain of `api-web` | Railway service settings. It is not part of `.railway/railway.ts` | own domain | own domain |

`apps/api/docs/configuration.md` describes each setting. The shared variables have the same names as the service variables, in the `__` form.

One Railway project has the two environments `staging` and `production`. Each environment has its own project token, its own shared variables and its own database. Two environments never share a database or a database role.

A PostgreSQL role belongs to the whole server, not to one database (PostgreSQL docs, "Database Roles"). The API's roles have fixed names, `api_owner` and `api_application`. So each environment needs its own PostgreSQL server. On Neon, use a separate project or a separate branch. A Neon branch starts as a copy of its parent, with the parent's data and role passwords. So do not branch staging from a production branch that holds data, and run `db/bootstrap.sql` on the new branch with new passwords. Two databases on one server would share the roles: `db/bootstrap.sql` for the second environment would reset the first environment's passwords, and the staging owner connection could change the production database.

## 4. First deployment of an environment

Set up `staging` first. Set up `production` later, when the product has users. Until then, leave `DEPLOY_PRODUCTION` unset, so the production jobs are skipped and `main` stays green.

Do these steps in this order for one environment. In the commands, `<environment>` is `staging` or `production`.

1. Create the Railway project once, if it does not exist. A new project has the environment `production`. Add the environment `staging` as an empty environment.
2. Create the database of the environment on its own PostgreSQL server (section 3), with its own owner and application roles. `apps/api/docs/setup.md` describes `db/bootstrap.sql`, which creates the roles.
3. In the Railway dashboard, open the shared variables of `<environment>`. Set each Railway shared variable of section 3. Set a secret as a sealed variable. Set a value in the Railway dashboard only, never in a file or a chat.
4. In the Railway project settings, create a project token for `<environment>`.
5. Apply the GitHub settings. This creates both GitHub environments, the production reviewer and the ruleset for release tags. One run is enough for both environments:

   ```bash
   pnpm github:apply
   ```

6. Store the two secrets in the GitHub environment. Each command prompts for the value and does not echo it:

   ```bash
   gh secret set RAILWAY_TOKEN --env <environment>
   gh secret set MIGRATIONS_CONNECTION_STRING --env <environment>
   ```

7. Install the Railway CLI, version 5.42.1 or newer. The Railway SDK in this repository needs that version. Then link the repository to the project and `<environment>`, and apply the Railway settings. The CLI compares the file with the linked environment only (Railway docs, "Infrastructure as Code"), so the same file serves both environments:

   ```bash
   railway login
   railway link
   pnpm railway:plan
   pnpm railway:apply
   ```

   The first plan creates `api-web` and `api-worker` and sets their variables. Read section 7 before you apply a later plan.

8. Generate a Railway domain for `api-web`: in the Railway dashboard, open the service in `<environment>`, Settings, Networking, "Generate Domain", port 8080. Do not accept port 8081: `PORT` is 8081 for Railway's health check, and the health endpoints answer only there. The worker needs no domain. Store the URL in the GitHub environment:

   ```bash
   gh variable set API_URL --env <environment> --body https://<the generated domain>
   ```

9. Make the GHCR package public, once. `publish-api` pushed it first when the deploy pipeline merged to `main`. `docs/github-settings.md`, section 5.4, gives the steps.
10. Start the first deploy:
    - For `staging`, switch the deploy on, then run the workflow on `main` again:

      ```bash
      gh variable set DEPLOY_STAGING --body true
      ```

      ```bash
      gh run list --workflow Delivery --branch main --limit 1
      ```

      ```bash
      gh run rerun <run id>
      ```

    - For `production`, switch the deploy on. The next push to `main` deploys to staging, then waits for the approval (section 2, "Approve a production deploy"):

      ```bash
      gh variable set DEPLOY_PRODUCTION --body true
      ```

`deploy.yml` then migrates the database, deploys the image and runs the smoke test. After the first production deploy, `tag-release` creates the first release tag.

### Switches

- `DEPLOY_STAGING`: `true` turns the staging deploy on. Without it, no deploy job and no tag job runs.
- `DEPLOY_PRODUCTION`: `true` turns the production deploy and the release tag on. Set it only after production is set up, when the product has users. To stop the production deploys, delete it with `gh variable delete DEPLOY_PRODUCTION`.

## 5. Versions

Each successful production deploy gets a release tag in Calendar Versioning: `vYYYY.0M.0D.N`, for example `v2026.10.11.1` (calver.org). The date is the UTC date of the deploy. `N` starts at 1 each day and counts the production deploys of that day.

- The job `tag-release` creates the tag after the production deploy passed. So a tag always names a version that ran in production.
- The tag is an annotated tag on the commit. Its message names the image digest. The same tag names the digest in GHCR.
- A release tag cannot be deleted or moved to another commit (`docs/github-settings.md`, section 3.5).
- Nobody creates or pushes a `v*` tag by hand.

Why CalVer and not Semantic Versioning: SemVer describes the compatibility of a public API that other software depends on (semver.org). A deployed service's version is a label for a deploy. The contract with clients is the `/v1` path, not the version number.

## 6. Roll back

### Production

Run `rollback.yml` on an earlier release tag:

```bash
gh workflow run rollback.yml --ref <release tag>
```

The workflow checks that the tag is a release tag, that its commit is on `main`, that the image of the commit exists in GHCR, and that its provenance comes from `delivery.yml` on `main`. Then the production job waits for the maintainer's approval, like any production deploy. It skips the check for an older commit, because a rollback deploys one on purpose.

The run uses `rollback.yml` and `deploy.yml` as they were at that tag.

### Staging

Revert the commit on `main` in a pull request. The merge deploys the reverted code to staging like any other change.

### The database

Never roll the database back. The earlier image finds no migration to apply, because the database already has all of its migrations. Fix a database problem forward, with a new migration.

A roll back works only to a version that the current schema supports. After a contract release (section 8), the version before the expand release no longer works.

Railway keeps the old deployment serving when a new one fails its health check. So a failed deploy needs no roll back. `deploy.yml` then fails and prints the deployment ID and its status.

## 7. Railway settings

`.railway/railway.ts` holds the Railway settings of the API as code (Railway docs, "Infrastructure as Code"). `railway.json` and `railway.toml` (Config as Code) are deprecated, and Railway stops reading them on 2026-12-01. Do not add them.

The maintainer applies the file by hand to each environment, like `pnpm github:apply`. `railway link` selects the environment:

```bash
pnpm railway:plan
pnpm railway:apply
```

`railway:plan` changes nothing. `railway:apply` asks before it applies a destructive change.

No setting differs between the environments today. If one must differ, the file can read `ctx.environment`.

Rules:

- Change a setting of the two services in the file, in a pull request. Do not change it only in the Railway dashboard. The next apply reverts it.
- Apply deletes a service variable that is not in the file. Add every service variable to the file.
- The file has no `source`, because CI sets the image. Railway CLI 5.63.1 compares the image that CI set with the missing `source` in the file, so a plan after a deploy can show a change to `source`. It is not known whether such an apply removes the image. Do not apply a plan that changes the `source` of a service. Report it instead.
- A shared variable must exist before you apply. A reference to a shared variable that does not exist gives an empty value. Railway does not stop the deploy for it. Source: Railway Central Station answers; the Railway docs do not describe this case. Set the shared variables first (step 3 of section 4).
- For most keys, an empty value stops the API at start, and the API names the key. Railway then marks the deployment as failed.
- An empty `ConnectionStrings__Messaging` does not stop the API. Wolverine then uses `ConnectionStrings__Database`, which goes through the pooler, and fails later at run time (`apps/api/docs/setup.md`). Always set it.
- An empty `Host__Cors__AllowedOrigins__0` is not a valid origin, so the API stops. Set it to the web app's origin, also before the web app exists.

## 8. Migrations and the running version

`deploy.yml` runs the migration while the old version still serves. The new version starts after the migration. So every migration must work with the version before it. A migration that breaks the previous version breaks production during the deploy.

Make a breaking change in two releases:

1. Expand: add the new column, table or constraint, and keep the old one. Release the code that writes both and reads the new one.
2. Contract: in a later release, after no running version uses the old one, remove it.

Staging runs each migration before production does, against its own database.

Sources: Humble and Farley, *Continuous Delivery*, chapter 12 ("Managing Data"). Ambler and Sadalage, *Refactoring Databases*, the transition period.

## 9. The migrations connection

`MIGRATIONS_CONNECTION_STRING` is the owner role's direct connection to the environment's database, in the Npgsql format:

```text
Host=<direct host>;Port=5432;Database=<database>;Username=api_owner;Password=<password>;SSL Mode=VerifyFull
```

- Use the owner role `api_owner` that `apps/api/db/bootstrap.sql` creates (R2 and R7 in `apps/api/docs/ARCHITECTURE.md`).
- Use the direct host, not the pooler. On Neon, the pooler's host name has `-pooler` in it.
- Use `SSL Mode=VerifyFull`. The client then checks the server's certificate and its host name.

Only the `migrate` command gets this connection (R3). It is a GitHub environment secret, and it never goes to Railway. The running services use the application role's connections in `ConnectionStrings__Database` and `ConnectionStrings__Messaging` (`apps/api/docs/setup.md`).

## 10. Deviations and known gaps

- **Staging runs no automated acceptance tests yet.** It runs only the smoke test of `deploy.yml`. *Continuous Delivery*, chapter 5, puts an automated acceptance test stage before a release. The row "Playwright end-to-end tests in CI" in `docs/github-settings.md`, section 6, closes this.
- **Anonymous callers share one rate limit on Railway.** The API limits a caller without a token by its IP address (`apps/api/src/Api/RateLimiting/UserRateLimiting.cs`). Behind Railway's proxy, every caller has the proxy's address, because no `ForwardedHeaders:KnownProxies` or `KnownNetworks` is set. So all anonymous calls to one pod share one limit, 1000 a minute by default. Heavy anonymous traffic can make the smoke test see 429, not 401. The maintainer decides whether to set the forwarded headers settings for Railway.

## 11. When a deploy, a tag or a rollback fails

- "The secret ... of the environment ... is empty": set the secret (section 4, step 6).
- "Production already runs a newer commit": the approved run is older than the commit that production runs. Reject it, and approve the newest waiting run. To go back on purpose, use the rollback (section 6).
- The migration fails: the step prints the error of the `migrate` command. Nothing was deployed.
- The Railway step fails: the step prints the Railway CLI's own message. Check `RAILWAY_TOKEN` and the service names.
- A deployment ends as `FAILED`, `CRASHED` or `REMOVED`: the step prints its ID. Read its logs in Railway. A missing shared variable or a private GHCR package are common causes.
- "Not healthy after 10 minutes" with "no new deployment yet": Railway started no deployment. This can happen when the services already run this digest.
- "The variable API_URL ... is empty": set it (section 4, step 8).
- The smoke test fails: the step prints the status codes. A 404 on `/v1/me/tenants` means the URL reaches a process that is not the web role. A 429 means the anonymous rate limit is used up (section 10).
- `tag-release` fails: production runs the new version, but it has no tag yet. Re-run the job. If the tag was created and only the GHCR step failed, the re-run creates the next `N` for the same commit.
- "... is not a release tag vYYYY.0M.0D.N" or "... is not on main": run `rollback.yml` on a release tag that `tag-release` created.
- "Delivery on main did not publish an image for this commit": the tag does not name a commit that `delivery.yml` published.
