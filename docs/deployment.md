# Deployment

This document describes how the API goes from `main` to staging, and from a release tag to production, on Railway. The web app goes to Vercel in a later step.

## 1. Purpose and sources

CI builds the API image once, tests it, scans it and publishes that same image. Each environment runs that image by digest. Railway builds nothing, and the repository is not connected to Railway.

Sources:

- Humble and Farley, *Continuous Delivery*, chapter 5: "Only build your binaries once", "Deploy the same way to every environment", "Smoke-test your deployments", "Deploy into a copy of production".
- The Twelve-Factor App, factor V ("Build, release, run") and factor III ("Config").
- SLSA v1.0, Build Level 2: provenance from a hosted build platform.
- Kief Morris, *Infrastructure as Code* (2nd ed.), chapter 2.
- Semantic Versioning 2.0.0 (semver.org).

## 2. The flow

| Trigger | Workflow | Result |
| --- | --- | --- |
| Pull request | `ci.yml` | Test, build, scan. Nothing is published or deployed. |
| Push to `main` | `ci.yml` | Test, build, scan, publish to GHCR, then deploy to **staging** through `deploy.yml` |
| Tag `vX.Y.Z` | `release.yml` | No build. Find the image of the tagged commit, check it, then deploy it to **production** through `deploy.yml` |

On a push to `main`:

1. The jobs `node`, `api`, `api-image` and `web-image` run. `api-image` builds the API image, scans it with Grype and runs it against PostgreSQL. It keeps the tested image as a workflow artifact for one day.
2. The job `CI` passes when every job before it passed.
3. `publish-api` pushes the tested image to GHCR as `ghcr.io/<owner>/<repo>/api`, with the tags `<commit SHA>` and `main`. It reads the digest from the registry. It attests the build provenance and an SPDX SBOM to that digest, and verifies both attestations.
4. `deploy-staging` calls `deploy.yml` for `staging`, only when the repository variable `DEPLOY_STAGING` is `true`.

On a tag `vX.Y.Z`, the job `promote` of `release.yml`:

1. Checks that the ref is a release tag `vX.Y.Z`. Pre-release tags, such as `v1.2.3-rc.1`, are not supported yet.
2. Checks that the tagged commit is on `main`.
3. Finds the image that `publish-api` tagged with the commit SHA. It builds nothing.
4. Verifies that the image's build provenance comes from `ci.yml`, run on `main`, in this repository.
5. Checks that staging deployed this commit with success. Production gets only a digest that staging ran.
6. Adds the tag `vX.Y.Z` to the same digest in GHCR. The digest does not change.

Then `deploy-production` calls `deploy.yml` for `production`.

`deploy.yml` holds the only copy of the deploy steps. In the GitHub environment that it gets:

1. It checks that the two secrets are set.
2. It runs `migrate` with the image digest.
3. It sets the digest as the image of `api-web` and `api-worker` in one Railway commit.
4. It waits until the new deployment of each service is `SUCCESS`.
5. It smoke-tests the public route: `GET /health/live` must answer 200, and `GET /v1/me/tenants` without a token must answer 401. The 401 shows that the route reaches the web process and its authentication.

Only one deploy to an environment runs at a time. GitHub never cancels a running deploy. When several deploys to one environment wait, GitHub keeps the newest one and cancels the older waiting ones. Only one release runs at a time.

## 3. What lives where

The table holds names only. A value never goes into the repository.

| Setting | Place | `staging` | `production` |
| --- | --- | --- | --- |
| The workflows, the image name, the Syft and Railway CLI image digests | Repository: `.github/workflows/` | same | same |
| The services `api-web` and `api-worker`, their region, replicas and health check | Repository: `.railway/railway.ts` | same | same |
| `Host__Role`, `ASPNETCORE_ENVIRONMENT`, `PORT` | Repository: `.railway/railway.ts`, as literal values | same | same |
| The names of the shared variables that each service uses | Repository: `.railway/railway.ts` | same | same |
| The environments, their deployment branches and tags, the ruleset for release tags | Repository: `scripts/github/apply-settings.sh`, `.github/rulesets/` and `docs/github-settings.md` | branch `main` | tags `v*` |
| `RAILWAY_TOKEN` | GitHub environment secret | the staging project token | the production project token |
| `MIGRATIONS_CONNECTION_STRING` | GitHub environment secret | the staging database | the production database |
| `API_URL` | GitHub environment variable | the staging URL of `api-web` | the production URL of `api-web` |
| `DEPLOY_STAGING` | GitHub repository variable | `true` switches the staging deploy on | not used: a release tag starts the deploy |
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
| The image digest that each service runs | Railway service settings. `deploy.yml` sets it | the latest commit on `main` | the latest release |
| The generated domain of `api-web` | Railway service settings. It is not part of `.railway/railway.ts` | own domain | own domain |

`apps/api/docs/configuration.md` describes each setting. The shared variables have the same names as the service variables, in the `__` form.

One Railway project has the two environments `staging` and `production`. Each environment has its own project token, its own shared variables and its own database. Two environments never share a database or a database role.

## 4. First deployment of an environment

Set up `staging` first. Set up `production` later, when the product has users. Until then, a release tag fails at the secrets check of `deploy.yml`. That is expected.

Do these steps in this order for one environment. In the commands, `<environment>` is `staging` or `production`.

1. Create the Railway project once, if it does not exist. A new project has the environment `production`. Add the environment `staging` as an empty environment.
2. Create the database of the environment, with its own owner and application roles. `apps/api/docs/setup.md` describes `db/bootstrap.sql`, which creates the roles.
3. In the Railway dashboard, open the shared variables of `<environment>`. Set each Railway shared variable of section 3. Set a secret as a sealed variable. Set a value in the Railway dashboard only, never in a file or a chat.
4. In the Railway project settings, create a project token for `<environment>`.
5. Apply the GitHub settings. This creates both GitHub environments and the ruleset for release tags. One run is enough for both environments:

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

8. Generate a Railway domain for `api-web`: in the Railway dashboard, open the service in `<environment>`, Settings, Networking, "Generate Domain", port 8080. The worker needs no domain. Store the URL in the GitHub environment:

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
      gh run list --workflow CI --branch main --limit 1
      ```

      ```bash
      gh run rerun <run id>
      ```

    - For `production`, release a commit that staging deployed with success (section 5).

`deploy.yml` then migrates the database, deploys the image and runs the smoke test.

## 5. Releasing

A release is the maintainer's decision. Only the maintainer creates and pushes a release tag.

1. Choose the version by Semantic Versioning: MAJOR for a change that breaks a client, MINOR for a new feature that keeps compatibility, PATCH for a fix.
2. Choose a commit on `main` that staging deployed with success. The environment `staging` on the repository's GitHub page lists its deployments.
3. Create an annotated tag on that commit and push it:

   ```bash
   git tag -a vX.Y.Z -m "<summary>" <commit SHA on main>
   ```

   ```bash
   git push origin vX.Y.Z
   ```

   The GitHub page "Draft a new release" works too. Choose the tag `vX.Y.Z` and the target commit there.

`release.yml` then promotes the image and deploys it to production. A release tag cannot be deleted or moved to another commit (`docs/github-settings.md`, section 3.5).

## 6. Roll back

### Production

Run `release.yml` again on an earlier release tag:

```bash
gh workflow run release.yml --ref <earlier tag>
```

The run uses `release.yml` and `deploy.yml` as they were at that tag. So a tag that is older than `release.yml` cannot be used for a rollback. The staging check passes for an earlier tag: GitHub marks an older staging deployment `inactive` after a newer one succeeds, and the check skips `inactive`.

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
- **The readiness endpoint is public.** Section 8 of `apps/api/docs/ARCHITECTURE.md` says that the readiness endpoint is not routed from outside. On Railway, the generated domain of `api-web` routes every path, so `/health/ready` is public too. Railway's health check needs this path. The deviations table of `ARCHITECTURE.md` does not record this yet. The maintainer decides how to close the gap.

## 11. When a deploy or a release fails

- "The secret ... of the environment ... is empty": set the secret (section 4, step 6). For `production` before the product has users, this is expected.
- The migration fails: the step prints the error of the `migrate` command. Nothing was deployed.
- The Railway step fails: the step prints the Railway CLI's own message. Check `RAILWAY_TOKEN` and the service names.
- A deployment ends as `FAILED`, `CRASHED` or `REMOVED`: the step prints its ID. Read its logs in Railway. A missing shared variable or a private GHCR package are common causes.
- "Not healthy after 10 minutes" with "no new deployment yet": Railway started no deployment. This can happen when the services already run this digest.
- "The variable API_URL ... is empty": set it (section 4, step 8).
- The smoke test fails: the step prints the status codes. A 404 on `/v1/me/tenants` means the URL reaches a process that is not the web role.
- "... is not a release tag vX.Y.Z" or "... is not on main": tag a commit of `main` with `vX.Y.Z`.
- "CI on main did not publish an image for this commit": the commit's CI run on `main` failed or has not finished.
- "Staging has not deployed this commit with success": wait for the staging deploy, then re-run the release.
