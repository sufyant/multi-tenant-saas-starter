# Deployment

This document describes how the API goes from `main` to Railway. The web app goes to Vercel in a later step.

## 1. Purpose and sources

CI builds the API image once, tests it, scans it and publishes that same image. Railway runs that image by digest. Railway builds nothing, and the repository is not connected to Railway.

Sources:

- Humble and Farley, *Continuous Delivery*, chapter 5: "Only build your binaries once".
- The Twelve-Factor App, factor V ("Build, release, run") and factor III ("Config").
- SLSA v1.0, Build Level 2: provenance from a hosted build platform.
- Kief Morris, *Infrastructure as Code* (2nd ed.), chapter 2.

## 2. The flow

On a push to `main`, `.github/workflows/ci.yml` does these steps:

1. The jobs `node`, `api`, `api-image` and `web-image` run. `api-image` builds the API image, scans it with Grype and runs it against PostgreSQL. It keeps the tested image as a workflow artifact for one day.
2. The job `CI` passes when every job before it passed.
3. `publish-api` pushes the tested image to GHCR as `ghcr.io/<owner>/<repo>/api`, with the tags `<commit SHA>` and `main`. It reads the digest from the registry. It attests the build provenance and an SPDX SBOM to that digest, and verifies both attestations.
4. `deploy-api` runs in the GitHub environment `production`, only when the repository variable `DEPLOY_TO_RAILWAY` is `true`. It runs `migrate` with the image digest. Then it sets that digest as the image of `api-web` and `api-worker` in one Railway commit. It waits until the new deployment of each service is `SUCCESS`.

On a pull request, steps 3 and 4 do not run.

Only one `deploy-api` runs at a time. GitHub never cancels a running deploy. When several pushes wait, GitHub keeps the newest one and cancels the older waiting ones.

## 3. What lives where

The table holds names only. A value never goes into the repository.

| Setting | Place |
| --- | --- |
| The workflow, the image name, the Syft and Railway CLI image digests | Repository: `.github/workflows/ci.yml` |
| The services `api-web` and `api-worker`, their region, replicas and health check | Repository: `.railway/railway.ts` |
| `Host__Role`, `ASPNETCORE_ENVIRONMENT`, `PORT` | Repository: `.railway/railway.ts`, as literal values |
| The names of the shared variables that each service uses | Repository: `.railway/railway.ts` |
| The environment `production` and its deployment branch `main` | Repository: `scripts/github/apply-settings.sh` and `docs/github-settings.md` |
| `RAILWAY_TOKEN` | GitHub environment secret of `production` |
| `MIGRATIONS_CONNECTION_STRING` | GitHub environment secret of `production` |
| `DEPLOY_TO_RAILWAY` | GitHub repository variable |
| `ConnectionStrings__Database` | Railway shared variable, sealed |
| `ConnectionStrings__Messaging` | Railway shared variable, sealed |
| `Authentication__Clerk__Issuer` | Railway shared variable |
| `Authentication__Clerk__AuthorizedParties__0` | Railway shared variable |
| `ControlPlane__FirstSystemAdminEmail` | Railway shared variable |
| `ControlPlane__Clerk__SecretKey` | Railway shared variable, sealed |
| `ControlPlane__Invitations__AcceptUrl` | Railway shared variable |
| `Notifications__Resend__ApiKey` | Railway shared variable, sealed |
| `Notifications__Resend__From` | Railway shared variable |
| `Host__Cors__AllowedOrigins__0` | Railway shared variable |
| The image digest that each service runs | Railway service settings. `deploy-api` sets it |
| The generated domain of `api-web` | Railway service settings. It is not part of `.railway/railway.ts` |

`apps/api/docs/configuration.md` describes each setting. The shared variables have the same names as the service variables, in the `__` form.

## 4. First deployment

Do these steps in this order. The deploy stays switched off until step 9, so `main` stays green.

1. Create the Railway project, if it does not exist. Its first environment is `production`. In the Railway dashboard, open the environment's shared variables. Set each Railway shared variable of section 3. Set a secret as a sealed variable. Set a value in the Railway dashboard only, never in a file or a chat.
2. In the Railway project settings, create a project token for the environment `production`.
3. Apply the GitHub settings. This creates the environment `production`:

   ```bash
   pnpm github:apply
   ```

4. Store the two secrets in the environment. Each command prompts for the value and does not echo it:

   ```bash
   gh secret set RAILWAY_TOKEN --env production
   gh secret set MIGRATIONS_CONNECTION_STRING --env production
   ```

5. Install the Railway CLI, version 5.42.1 or newer. The Railway SDK in this repository needs that version. Then link the repository to the project and the environment `production`, and apply the Railway settings:

   ```bash
   railway login
   railway link
   pnpm railway:plan
   pnpm railway:apply
   ```

   The first plan creates `api-web` and `api-worker` and sets their variables. Read section 5 before you apply a later plan.

6. Generate a Railway domain for `api-web`: in the Railway dashboard, open the service, Settings, Networking, "Generate Domain", port 8080. The worker needs no domain.
7. Make the GHCR package public. `publish-api` pushed it first when this change merged to `main`. `docs/github-settings.md`, section 5.4, gives the steps.
8. Switch the deploy on:

   ```bash
   gh variable set DEPLOY_TO_RAILWAY --body true
   ```

9. Run the workflow on `main` again. Find the newest run, then re-run it:

   ```bash
   gh run list --workflow CI --branch main --limit 1
   ```

   ```bash
   gh run rerun <run id>
   ```

`deploy-api` then migrates the database and deploys the image. Check that `https://<the generated domain>/health/live` answers `Healthy`.

## 5. Railway settings

`.railway/railway.ts` holds the Railway settings of the API as code (Railway docs, "Infrastructure as Code"). `railway.json` and `railway.toml` (Config as Code) are deprecated, and Railway stops reading them on 2026-12-01. Do not add them.

The maintainer applies the file by hand, like `pnpm github:apply`:

```bash
pnpm railway:plan
pnpm railway:apply
```

`railway:plan` changes nothing. `railway:apply` asks before it applies a destructive change.

Rules:

- Change a setting of the two services in the file, in a pull request. Do not change it only in the Railway dashboard. The next apply reverts it.
- Apply deletes a service variable that is not in the file. Add every service variable to the file.
- The file has no `source`, because CI sets the image. Railway CLI 5.63.1 compares the image that CI set with the missing `source` in the file, so a plan after a deploy can show a change to `source`. It is not known whether such an apply removes the image. Do not apply a plan that changes the `source` of a service. Report it instead.
- A shared variable must exist before you apply. A reference to a shared variable that does not exist gives an empty value. Railway does not stop the deploy for it. Source: Railway Central Station answers; the Railway docs do not describe this case. Set the shared variables first (step 1 of section 4).
- For most keys, an empty value stops the API at start, and the API names the key. Railway then marks the deployment as failed.
- An empty `ConnectionStrings__Messaging` does not stop the API. Wolverine then uses `ConnectionStrings__Database`, which goes through the pooler, and fails later at run time (`apps/api/docs/setup.md`). Always set it.
- An empty `Host__Cors__AllowedOrigins__0` is not a valid origin, so the API stops. Set it to the web app's origin, also before the web app exists.

## 6. The migrations connection

`MIGRATIONS_CONNECTION_STRING` is the owner role's direct connection, in the Npgsql format:

```text
Host=<direct host>;Port=5432;Database=<database>;Username=api_owner;Password=<password>;SSL Mode=VerifyFull
```

- Use the owner role `api_owner` that `apps/api/db/bootstrap.sql` creates (R2 and R7 in `apps/api/docs/ARCHITECTURE.md`).
- Use the direct host, not the pooler. On Neon, the pooler's host name has `-pooler` in it.
- Use `SSL Mode=VerifyFull`. The client then checks the server's certificate and its host name.

Only the `migrate` command gets this connection (R3). It is a GitHub environment secret, and it never goes to Railway. The running services use the application role's connections in `ConnectionStrings__Database` and `ConnectionStrings__Messaging` (`apps/api/docs/setup.md`).

## 7. Roll back

To roll back, deploy an earlier digest:

1. In GitHub Actions, open an earlier run on `main` whose `deploy-api` passed.
2. Re-run its job `deploy-api`. The job uses the digest from that run's `publish-api`. GitHub lets you re-run a run for 30 days.

Never roll the database back. The earlier image finds no migration to apply, because the database already has all of its migrations. Fix a database problem forward, with a new migration.

A roll back works only to a version that the current schema supports. After a contract release (section 8), the version before the expand release no longer works.

Railway keeps the old deployment serving when a new one fails its health check. So a failed deploy needs no roll back. `deploy-api` then fails and prints the deployment ID and its status.

## 8. Migrations and the running version

`deploy-api` runs the migration while the old version still serves. The new version starts after the migration. So every migration must work with the version before it. A migration that breaks the previous version breaks production during the deploy.

Make a breaking change in two releases:

1. Expand: add the new column, table or constraint, and keep the old one. Release the code that writes both and reads the new one.
2. Contract: in a later release, after no running version uses the old one, remove it.

Sources: Humble and Farley, *Continuous Delivery*, chapter 12 ("Managing Data"). Ambler and Sadalage, *Refactoring Databases*, the transition period.

## 9. Known gap: the readiness endpoint is public

Section 8 of `apps/api/docs/ARCHITECTURE.md` says that the readiness endpoint is not routed from outside. On Railway, the generated domain of `api-web` routes every path, so `/health/ready` is public too. Railway's health check needs this path. The deviations table of `ARCHITECTURE.md` does not record this yet. The maintainer decides how to close the gap.

## 10. When `deploy-api` fails

- "The secret ... is empty": set the secret (section 4, step 4).
- The migration fails: the step prints the error of the `migrate` command. Nothing was deployed.
- The Railway step fails: the step prints the Railway CLI's own message. Check `RAILWAY_TOKEN` and the service names.
- A deployment ends as `FAILED`, `CRASHED` or `REMOVED`: the step prints its ID. Read its logs in Railway. A missing shared variable or a private GHCR package are common causes.
- "Not healthy after 10 minutes" with "no new deployment yet": Railway started no deployment. This can happen when the services already run this digest.
