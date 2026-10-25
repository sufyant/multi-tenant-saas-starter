# Deployment

This document describes how the API, the web app and the platform app go from `main` to staging, and after the maintainer's approval to production, on Railway.

## 1. Purpose and sources

The pipeline builds each image once (the API, the web app and the platform app), tests it, scans it and publishes that same image. Each environment runs those images by digest. No image holds a value that differs between environments: each reads its settings at run time. Railway builds nothing, and the repository is not connected to Railway.

Sources:

- Humble and Farley, *Continuous Delivery*, chapter 5: "Only build your binaries once", "Deploy the same way to every environment", "Smoke-test your deployments", "Deploy into a copy of production", and the push-button release.
- The Twelve-Factor App, factor V ("Build, release, run") and factor III ("Config").
- SLSA v1.0, Build Level 2: provenance from a hosted build platform.
- Kief Morris, *Infrastructure as Code* (2nd ed.), chapter 2.
- Calendar Versioning (calver.org).

## 2. The flow

| File | Trigger | Jobs |
| --- | --- | --- |
| `ci.yml` | `pull_request`, and `workflow_call` from `delivery.yml` | Verification only: `node`, `api`, `generated`, `api-image`, `web-image`, `platform-image`, `e2e`, `dependency-review`, and the aggregator `CI`. No publish, no deploy. |
| `delivery.yml` | `push` to `main` | Calls `ci.yml`, then `publish-api`, `publish-web` and `publish-platform`, then `deploy-staging`, `deploy-production`, `tag-release`, in this order |
| `publish-image.yml` | `workflow_call` | The only copy of the publish steps, for each of the three images |
| `deploy.yml` | `workflow_call` | The only copy of the deploy steps, for staging and production |
| `rollback.yml` | `workflow_dispatch` on a release tag | No build. Deploys the three images of that release (`api`, `web`, `platform`) to **production**, after the approval |

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

On a push to `main`, after `ci.yml` passed:

1. `publish-api`, `publish-web` and `publish-platform` call `publish-image.yml`. Each pushes its tested image to GHCR as `ghcr.io/<owner>/<repo>/<app>` (`api`, `web` or `platform`), with the tags `<commit SHA>` and `main`. It reads the digest from the registry. It attests the build provenance and an SPDX SBOM to that digest, and verifies both attestations. The signer of the attestations is `publish-image.yml` (GitHub Docs, "Using artifact attestations and reusable workflows to achieve SLSA v1 Build Level 3").
2. `deploy-staging` calls `deploy.yml` for `staging` with the three digests, when the repository variable `DEPLOY_STAGING` is `true`.
3. `deploy-production` waits for the maintainer's approval, then calls `deploy.yml` for `production` with the same three digests. It runs only when `deploy-staging` passed and the repository variable `DEPLOY_PRODUCTION` is `true`.
4. `tag-release` runs only when `deploy-production` passed. It creates the release tag on the commit and on each image digest in GHCR (section 5).

`deploy.yml` holds the only copy of the deploy steps. In the GitHub environment that it gets:

1. It checks that the three secrets are set.
2. For `production`, it checks that the commit is not older than the commit that production runs (section 6). A rollback skips this check.
3. It runs `migrate` with the API image's digest.
4. It sets that digest as the image of `api-web` and `api-worker` in one Railway commit.
5. It waits until the new deployment of each API service is `SUCCESS`.
6. It smoke-tests the API's public route: `GET /v1/me/tenants` without a token must answer 401, and `GET /health/ready` must answer 404. The 401 shows that the route reaches the web process and its authentication. The 404 shows that the public route does not reach the health endpoints: they answer only on the management port, 8081. Railway's health check covers readiness there.
7. It sets the web app's digest on `web` and the platform app's digest on `platform`, one Railway commit for each. The web apps go after the API, so a web app never runs against an API older than the one it was tested with. One exception: a rollback to a release tagged before the web apps were deployed rolls back the API only (section 6).
8. It waits until the new deployment of `web` and `platform` is `SUCCESS`. Railway's health check calls `GET /health` of each (section 13).
9. It smoke-tests each app's public route: `GET /health` must answer 200, and `GET /` without a session must end at the app's `/sign-in` with 200. The request asks for HTML, keeps the cookies and follows the redirects, as a browser's first visit does. With a Clerk development instance, Clerk first sends the request through its Frontend API and back, so this step also shows that the instance accepts the domain. Each attempt prints the path and the status where the request ended, curl's exit code (28 is a timeout), and the host where the redirect chain stopped: `stopped at the app`, or `stopped at a third party: <host>`. It prints the host only, never the query or a cookie.

A re-run of a Delivery run is safe, and it passes when the images are already deployed. Before the Railway commit of steps 4 and 7, `deploy-to-railway.sh` reads the newest deployment of each service (by `createdAt`, any status). When its image (`meta.image`, with the digest) is the image to deploy, the commit would change nothing, and Railway would make no new deployment. So the script leaves that service out of the commit and prints `<service>: deployment <id> already has this image`. Steps 5 and 8 then wait for that deployment: `SUCCESS` passes at once, a deployment in progress is waited for, and `FAILED`, `CRASHED` or `REMOVED` fails at once. When no service of an app needs the commit, the script skips it. Only the newest deployment counts: after `X`, then `Y`, a deploy of `X` makes a new deployment.

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
| The services `api-web` and `api-worker`, their region, replicas and health check | Repository: `.railway/railway.ts`. The service names, the management port and the health path are in `.railway/api.json`, which the deploy scripts read too | same | same |
| The services `web` and `platform`, their region, replicas, health check and variables | Repository: `.railway/railway.ts`. Each app's service name, port and health path are in `.railway/web.json` and `.railway/platform.json`, which the deploy scripts and the apps' tests read too | same | same |
| The PostgreSQL service `postgres`: Railway's template, its image `postgres-ssl:18` and its region | Repository: `.railway/railway.ts` | same | same |
| Public access (TCP proxy) of `postgres` | Railway service settings, by hand (section 4, step 7) | own proxy | own proxy |
| Backups and point-in-time recovery of `postgres` | Railway, by hand with the Railway CLI (section 4, step 11) | off | on |
| `Host__Role`, `PORT` | Repository: `.railway/railway.ts`, as literal values | same | same |
| `ASPNETCORE_ENVIRONMENT` of `api-web` and `api-worker` | Repository: `.railway/railway.ts`, from the linked Railway environment (`ctx.environment`). The API names the environment of its error events from it, in lower case | `Staging` | `Production` |
| `API_URL` of `web` and `platform`: `http://${{api-web.RAILWAY_PRIVATE_DOMAIN}}:8080` | Repository: `.railway/railway.ts`, as a Railway reference. Railway resolves it in each environment | own private domain | own private domain |
| The names of the shared variables that each service uses | Repository: `.railway/railway.ts` | same | same |
| The environments, their reviewers, their deployment branches and tags, the ruleset for release tags | Repository: `scripts/github/apply-settings.sh`, `.github/rulesets/` and `docs/github-settings.md` | branch `main`, no reviewer | branch `main` and tags `v*`, the owner as reviewer |
| `RAILWAY_TOKEN` | GitHub environment secret | the staging project token | the production project token |
| `MIGRATIONS_CONNECTION_STRING` | GitHub environment secret | the staging database, through its TCP proxy | the production database, through its TCP proxy |
| `MIGRATIONS_ROOT_CERTIFICATE` | GitHub environment secret | the root certificate of the staging database | the root certificate of the production database |
| `API_URL`, `WEB_URL`, `PLATFORM_URL` | GitHub environment variables. The smoke tests of `deploy.yml` read them | `https://<api domain>`, `https://<web domain>`, `https://<platform domain>` of staging | the same, of production |
| `DEPLOY_STAGING`, `DEPLOY_PRODUCTION` | GitHub repository variables | `DEPLOY_STAGING=true` switches the staging deploy on | `DEPLOY_PRODUCTION=true` switches the production deploy and the release tag on |
| Each name in `sharedVariables` of `.railway/railway.ts`, and `CLERK_PUBLISHABLE_KEY`. Three are secrets: `ConnectionStrings__Database`, `ControlPlane__Clerk__SecretKey` and `Notifications__Resend__ApiKey` | Railway shared variable. A secret is a sealed variable | own value | own value |
| The image digest that each service runs | Railway service settings. `deploy.yml` sets it | the latest commit on `main` | the latest approved commit, or a rolled back release |
| The custom domains of `api-web`, `web` and `platform`, and their DNS records | Railway service settings and the maintainer's DNS. Not part of the repository (section 13) | own domains | own domains |
| The Errors applications, the uptime monitors and the heartbeats | Repository: `.betterstack/config.json`. `pnpm betterstack:apply` applies it (section 16) | monitors `staging api`, `staging web`, `staging platform`, heartbeat `staging api-worker`; the applications are shared | monitors `production api`, `production web`, `production platform`, heartbeat `production api-worker` |
| `Host__ErrorTracking__Dsn`, `WEB_ERROR_TRACKING_DSN`, `PLATFORM_ERROR_TRACKING_DSN` | Railway shared variable, sealed. Copied from each Errors application's "Ingest" tab (section 16, step 5) | the same DSN in both environments | the same DSN in both environments |
| `Host__Heartbeat__Url` | Railway shared variable, sealed: its token is in the path. Copied from the heartbeat `<environment> api-worker` (section 16, step 5). `.railway/railway.ts` gives it to `api-worker` only. Optional: without it, the worker sends no heartbeat | the URL of `staging api-worker` | the URL of `production api-worker` |
| `ERROR_TRACKING_ENVIRONMENT` of `web` and `platform` | Repository: `.railway/railway.ts`, from the linked Railway environment (`ctx.environment`), as it sets the API's `ASPNETCORE_ENVIRONMENT` | `staging` | `production` |
| The Errors integrations "Incidents" and "Slack": the Slack channel and the alerts | Better Stack, by hand (section 16, step 6) | shared | shared |
| `BETTERSTACK_UPTIME_TOKEN`, `BETTERSTACK_TELEMETRY_TOKEN` | The maintainer's password manager only (section 16, step 2) | shared | shared |

`apps/api/docs/configuration.md` describes each setting. The shared variables have the same names as the service variables, in the `__` form.

One Railway project has the two environments `staging` and `production`. Each environment has its own project token, its own shared variables and its own PostgreSQL service. Two environments never share a database server, a database role or data.

A PostgreSQL role belongs to the whole server, not to one database (PostgreSQL docs, "Database Roles"). The API's roles have fixed names, `api_owner` and `api_application`. So each environment needs its own PostgreSQL server. Two databases on one server would share the roles: `db/bootstrap.sql` for the second environment would reset the first environment's passwords, and the staging owner connection could change the production database.

Each environment gets its own PostgreSQL service `postgres`, from Railway's PostgreSQL template, with its own volume. `.railway/railway.ts` declares it, so `pnpm railway:apply` creates it in the linked environment. `db/bootstrap.sql` runs in each environment with new passwords. Railway's environment sync does not copy a database: "Sync copies configuration, not data" (Railway docs, "Isolate Staging from Production for Compliance").

Another PostgreSQL provider works too, because the API uses plain PostgreSQL. `apps/api/docs/setup.md` holds the notes for Railway and for Neon, the alternative.

## 4. First deployment of an environment

Set up `staging` first. Set up `production` later, when the product has users. Until then, leave `DEPLOY_PRODUCTION` unset, so the production jobs are skipped and `main` stays green.

Do these steps in this order for one environment. In the commands, `<environment>` is `staging` or `production`.

1. Create the Railway project once, if it does not exist. A new project has the environment `production`. Add the environment `staging` as an empty environment.
2. Make two new passwords for the environment: one for `api_owner` and one for `api_application`. Keep them in a password manager. Never use a password of another environment (section 3). On macOS, this command makes a password and copies it to the clipboard. It prints nothing. A hex password has no character that a connection string treats in a special way. Run it once for each password, and paste each into the password manager:

   ```bash
   openssl rand -hex 32 | pbcopy
   ```

3. In the Railway dashboard, open the shared variables of `<environment>`. Set each Railway shared variable of section 3. Set a secret as a sealed variable. Set a value in the Railway dashboard only, never in a file or a chat. `ConnectionStrings__Database` uses the application password of step 2, in the format of section 9. The database does not exist yet, but its private host is known.
4. In the Railway project settings, create a project token for `<environment>`.
5. Apply the GitHub settings. This creates both GitHub environments, the production reviewer and the ruleset for release tags. One run is enough for both environments:

   ```bash
   pnpm github:apply
   ```

6. Install the Railway CLI, version 5.42.1 or newer. The Railway SDK in this repository needs that version. Then link the repository to the project and `<environment>`, and apply the Railway settings. The CLI compares the file with the linked environment only (Railway docs, "Infrastructure as Code"), so the same file serves both environments:

   ```bash
   railway login
   railway link
   pnpm railway:plan
   pnpm railway:apply
   ```

   The first plan creates `postgres`, `api-web`, `api-worker`, `web` and `platform`. Railway attaches a volume to `postgres`. The other services get no image until the first deploy, so they do not start yet. Read section 7 before you apply a later plan.

7. Turn on public access for the database. In the Railway dashboard, open the `postgres` service in `<environment>`, then Settings. In the section Networking, open Public access and press Add Public Access. Add it on port 5432: this is a TCP proxy. Railway shows a proxy domain and a port, for example `shuttle.proxy.rlwy.net:15140`. Note both. The bootstrap (step 10) and the `migrate` command of `deploy.yml` connect through it. Why by hand: the `postgres` helper of `railway/iac` accepts only a region, so `.railway/railway.ts` cannot hold a TCP proxy for it.
8. Check `postgres` in the dashboard: it runs in `europe-west4`, next to the API, from the image `ghcr.io/railwayapp-templates/postgres-ssl:18`, with a volume on `/var/lib/postgresql/data`.
9. Download the root certificate of the environment's database (section 9). Reading the file changes nothing in the database.

   `railway ssh` needs an SSH key that is registered with Railway (Railway docs, "railway ssh", "Manage SSH keys"). If the machine has no SSH key, make one first:

   ```bash
   ssh-keygen -t ed25519
   ```

   Register the public key with Railway. `<name>` names the machine, for example `laptop`:

   ```bash
   railway ssh keys add --key ~/.ssh/id_ed25519.pub --name <name>
   ```

   Write the certificate outside the repository, in a folder for the project's keys. `<project>` is the name of the project. Steps 10 and 12 read the file from this path:

   ```bash
   mkdir -p ~/<project>-keys
   ```

   ```bash
   railway ssh --service postgres -- cat /var/lib/postgresql/data/certs/root.crt > ~/<project>-keys/<environment>-database-root.crt
   ```

   ```bash
   openssl x509 -in ~/<project>-keys/<environment>-database-root.crt -noout -subject -enddate
   ```

   The second command prints `subject=CN=root-ca` and the end date.

10. Run `apps/api/db/bootstrap.sql` once, as the template's superuser `postgres`, through the TCP proxy, from the repository root. Docker runs `psql` 18, so no local `psql` is needed. First read the three passwords. `read -s` does not echo them, and they never reach the shell history. The password of `postgres` is in the Railway dashboard: the service `postgres`, Variables, `POSTGRES_PASSWORD`. Copy it. Do not press Regenerate. The other two are the passwords of step 2. The password of `api_application` must be the one in `ConnectionStrings__Database` (step 3). If it differs, the API cannot connect:

    ```bash
    printf 'Password of postgres: '; read -rs PGPASSWORD; echo
    printf 'New password of api_owner: '; read -rs owner_password; echo
    printf 'New password of api_application: '; read -rs application_password; echo
    export PGPASSWORD owner_password application_password
    ```

    Then run the script. Replace `<proxy domain>` and `<proxy port>` with the values of step 7:

    ```bash
    docker run --rm -e PGPASSWORD -e owner_password -e application_password \
      -v "$PWD/apps/api/db/bootstrap.sql:/bootstrap.sql:ro" \
      -v "$HOME/<project>-keys/<environment>-database-root.crt:/root.crt:ro" \
      postgres:18 sh -c 'exec psql "host=<proxy domain> port=<proxy port> dbname=railway user=postgres sslmode=verify-ca sslrootcert=/root.crt" -v owner_password="$owner_password" -v application_password="$application_password" -f /bootstrap.sql'
    ```

    Then check the application role. The command connects as `api_application` through the proxy, with its password and `verify-ca`:

    ```bash
    PGPASSWORD="$application_password" docker run --rm -e PGPASSWORD \
      -v "$HOME/<project>-keys/<environment>-database-root.crt:/root.crt:ro" \
      postgres:18 psql "host=<proxy domain> port=<proxy port> dbname=railway user=api_application sslmode=verify-ca sslrootcert=/root.crt" \
      -At -F ' | ' -c "select current_user, rolsuper, rolbypassrls from pg_roles where rolname = current_user"
    ```

    The expected output is `api_application | f | f`: the role can log in with the password of `ConnectionStrings__Database`, it is not a superuser, and it does not bypass RLS. If the output differs, stop and find the cause before the next step.

    ```bash
    unset PGPASSWORD owner_password application_password
    ```

    `docker run -e NAME` copies the variable from the shell, so no password is on the command line of the host. `sslmode=verify-ca` checks the server's certificate against the root certificate of step 9 (section 9). The script changes roles through SQL, so the database leaves Railway's database support scope (`apps/api/docs/setup.md`).

11. Production only: turn on the backups and point-in-time recovery of the database. With the CLI linked to `production`:

    ```bash
    railway postgres pitr schedule set --service postgres --daily --weekly
    ```

    ```bash
    railway postgres pitr enable --service postgres
    ```

    ```bash
    railway postgres pitr status --service postgres
    ```

    The schedule keeps a daily backup for 6 days and a weekly backup for 27 days (Railway docs, "Backups"). Point-in-time recovery archives every WAL segment to a bucket `Postgres-PITR` and keeps about 4 weeks (Railway docs, "Point-in-Time Recovery"). Enabling it sets archive variables on `postgres` and redeploys it. Why by hand: `.railway/railway.ts` cannot hold them, because the `postgres` helper accepts only a region. Read section 7 before the next apply. Staging gets no backups: its data can be rebuilt.

12. Store the three secrets in the GitHub environment. The first command prompts for the token and does not echo it:

    ```bash
    gh secret set RAILWAY_TOKEN --env <environment>
    ```

    `MIGRATIONS_CONNECTION_STRING` uses the owner password of step 2 and the proxy of step 7, in the format of section 9. `read -rs` reads the password without an echo. `printf` is a shell builtin, so the password is on no command line, and the history keeps only the variable's name. Replace `<proxy domain>` and `<proxy port>`:

    ```bash
    printf 'Password of api_owner: '; read -rs owner_password; echo
    ```

    ```bash
    printf 'Host=%s;Port=%s;Database=railway;Username=api_owner;Password=%s;SSL Mode=VerifyCA' \
      '<proxy domain>' '<proxy port>' "$owner_password" | gh secret set MIGRATIONS_CONNECTION_STRING --env <environment>
    ```

    ```bash
    unset owner_password
    ```

    The last secret is the file of step 9:

    ```bash
    gh secret set MIGRATIONS_ROOT_CERTIFICATE --env <environment> < ~/<project>-keys/<environment>-database-root.crt
    ```

13. Add a custom domain to `api-web`, `web` and `platform`, and store their URLs in the GitHub environment. Section 13, "Custom domains", gives the steps. Port 8081 of `api-web` must never get a domain: `PORT` is 8081 for Railway's health check, and the health endpoints answer only there. The worker needs no domain.
14. Make the three GHCR packages public, once: `api`, `web` and `platform`. `publish-image.yml` pushed each first when its pipeline merged to `main`. `docs/github-settings.md`, section 5.4, gives the steps.
15. Start the first deploy:
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
- The tag is an annotated tag on the commit. Its message names the three image digests. The same tag names each digest in GHCR.
- A release tag cannot be deleted or moved to another commit (`docs/github-settings.md`, section 3.5).
- Nobody creates or pushes a `v*` tag by hand.

Why CalVer and not Semantic Versioning: SemVer describes the compatibility of a public API that other software depends on (semver.org). A deployed service's version is a label for a deploy. The contract with clients is the `/v1` path, not the version number.

## 6. Roll back

### Production

Run `rollback.yml` on an earlier release tag:

```bash
gh workflow run rollback.yml --ref <release tag>
```

The workflow checks that the tag is a release tag, that its commit is on `main`, that the three images of the commit (`api`, `web` and `platform`) exist in GHCR, and that their provenance comes from `publish-image.yml` on `main`. It rolls the three back together. Then the production job waits for the maintainer's approval, like any production deploy. It skips the check for an older commit, because a rollback deploys one on purpose.

The run uses `rollback.yml` and `deploy.yml` as they were at that tag. A release tagged before the web apps were deployed rolls back the API only, and checks `delivery.yml` as the signer, as it did then. The web apps then keep running against the older API.

### Staging

Revert the commit on `main` in a pull request. The merge deploys the reverted code to staging like any other change.

### The database

Never roll the database back. The earlier image finds no migration to apply, because the database already has all of its migrations. Fix a database problem forward, with a new migration.

A roll back works only to a version that the current schema supports. After a contract release (section 8), the version before the expand release no longer works.

Each module's migrations start with one initial migration. A version from before those initial migrations knows other migrations, so the current schema does not support it: its `migrate` step fails, and the deploy stops before Railway gets its image. A rollback to such a version, or a revert of the initial migrations on `main`, does not work.

Railway keeps the old deployment serving when a new one fails its health check. So a failed deploy needs no roll back. `deploy.yml` then fails and prints the deployment ID and its status.

## 7. Railway settings

`.railway/railway.ts` holds the Railway settings of the API and the web apps as code (Railway docs, "Infrastructure as Code"). `railway.json` and `railway.toml` (Config as Code) are deprecated, and Railway stops reading them on 2026-12-01. Do not add them.

The maintainer applies the file by hand to each environment, like `pnpm github:apply`. `railway link` selects the environment:

```bash
pnpm railway:plan
pnpm railway:apply
```

`railway:plan` changes nothing. When the plan has destructive lines, such as a deleted variable or a `source` going to `null`, apply needs `--confirm-destructive`. Add it only after you read the plan and every line in it is expected:

```bash
pnpm railway:apply --confirm-destructive
```

Only the environment names differ between the environments: the API's `ASPNETCORE_ENVIRONMENT` and the web apps' `ERROR_TRACKING_ENVIRONMENT`, which the file reads from `ctx.environment`. Any other setting that must differ can read it too.

Rules:

- Change a setting of a service in the file, in a pull request. Do not change it only in the Railway dashboard. The next apply reverts it.
- Apply deletes a service variable that is not in the file. Add every service variable to the file.
- No service but `postgres` has a `source` in the file, because CI sets their image. After the first deploy, every plan shows `source.image` and `source.type` of each deployed service going to `null`. The Railway CLI diffs a `source` that the file leaves out against `null`: `diff_top_level_field` in `src/iac/change_set.rs` of github.com/railwayapp/cli. CLI 5.64.2 shows it, and the code is the same in 5.68.0. The CLI has no setting that leaves `source` unmanaged; `variables: { managed, ignore }` covers variables only (`variable_policy` in `src/iac/compiler.rs`). The Railway docs say the opposite: "Omit `source` when `.railway/railway.ts` should manage service settings but not declare a GitHub repository or Docker image" (Railway docs, "Infrastructure as Code", reference, "Source"). Observed in staging on 2026-10-19, with Railway CLI 5.64.2: `pnpm railway:apply --confirm-destructive` applied a plan with these `source` lines and kept the image. The variables of `api-web` and `api-worker` changed, so Railway redeployed both with the same `ghcr.io/<owner>/<repo>/api` image, and both came online (section 13, step 2). One observation is not a guarantee for another CLI version. So after the first deploy, still add settings with the procedure below when you can. A plan that adds a service needs an apply: apply only a plan that shows exactly the expected lines, and look at the image of each deployed service right after it. If an apply removed an image, rerun the last successful Delivery run of `main` at once, so `deploy.yml` sets the image again by digest.
- Production has not had an apply since its first deploy. The first apply to production after it goes live is a decision for the maintainer.
- The first apply of a new environment is safe (step 6 of section 4). The services have no image yet, so the plan shows no change to `source`.
- A shared variable must exist before you apply. A reference to a shared variable that does not exist gives an empty value. Railway does not stop the deploy for it. Source: Railway Central Station answers; the Railway docs do not describe this case. Set the shared variables first (step 3 of section 4).
- For most keys, an empty value stops the API at start, and the API names the key. Railway then marks the deployment as failed.
- The file declares `postgres` with the `postgres` helper of `railway/iac`, but Railway's own flows set its volume, its TCP proxy, its backups and its point-in-time recovery (the bucket `Postgres-PITR` and the archive variables). The Railway docs do not say how apply treats these. Do not apply a plan that removes or changes one of them. Report it instead.
- Do not apply a plan that changes the image of `postgres`. A new major version needs Railway's upgrade flow, not a new image tag (Railway docs, "Upgrading PostgreSQL Major Versions", "Manual Image Edits"). Point-in-time recovery needs a major tag such as `:18`, not a minor tag (Railway docs, "Point-in-Time Recovery", "Limitations").
- An environment that was set up before step 18f can still have the shared variable `ConnectionStrings__Messaging`. No service references it any more. Delete it in the Railway dashboard.

Add a shared variable to the API services without apply:

1. Add its name to `sharedVariables` in `.railway/railway.ts`, in a pull request.
2. Before the merge, set the shared variable in each environment, as in step 3 of section 4.
3. Before the merge, in the Railway dashboard, open Shared Variables, choose "Configure Shared Variable" for the new variable, and tick `api-web` and `api-worker`.
4. Read the plan first: on the pull request branch, run `pnpm railway:plan`. It must show only the two `source` changes, one for `api-web` and one for `api-worker`. Any other line means the file and Railway differ. Fix that first, and do not apply.
5. Merge.

Steps 2 and 3 are safe before the merge, because the running version ignores a variable it does not read. The order matters when the same pull request adds code that needs the variable at start: without the variable, the staging deploy fails.

Remove a shared variable from the API services without apply:

1. Remove its name from `sharedVariables` in `.railway/railway.ts`, in the pull request that stops reading it. `pnpm railway:plan` then also shows "Delete variable" for `api-web` and `api-worker`.
2. Merge, and let the deploy finish in each environment. The version before it may still need the variable to start.
3. Then, in the Railway dashboard, open Shared Variables, untick `api-web` and `api-worker` for the variable, and delete it.

Step 20i removed `ControlPlane__Clerk__WebhookSigningSecret` this way. The step that added the web apps removed `Host__Cors__AllowedOrigins__0` with its apply (section 13).

Rename a configuration key of the API without apply. The API reads a key only by its new name, so the value must exist under the new name before the new version starts:

1. In the pull request that renames the key, rename it in `sharedVariables` in `.railway/railway.ts`.
2. Before the merge, in each environment, add a shared variable with the new name and the same value, and tick `api-web` and `api-worker` for it (steps 2 and 3 of "Add a shared variable"). The running version ignores it.
3. Merge, and let the deploy finish in each environment. The version before it may still need the old name to start, also after a rollback (section 6).
4. Then untick `api-web` and `api-worker` for the old shared variable and delete it (step 3 of "Remove a shared variable").

Step r1 renamed these keys. Each one is optional and has a default, and no entry of `sharedVariables` names one, so no environment needs a step:

| Old key | New key |
| --- | --- |
| `ControlPlane:Clerk:RetryDelays` | `ControlPlane:IdentityProvider:RetryDelays` |
| `Api:IdempotencyKeyRetention` | `Host:Idempotency:KeyRetention` |
| `RateLimiting:...` | `Host:RateLimiting:...` |
| `Messaging:UnknownMessages:...` | `Host:Messaging:UnknownMessages:...` |
| `Notifications:InvitationEmailRetryDelays` | `Notifications:InvitationEmails:RetryDelays` |

`ControlPlane__Clerk__SecretKey` keeps its name. If an environment has a shared variable with an old name, no service references it, so the API never read it: delete it in the Railway dashboard.

Step r5 renamed these keys. Each one is required, or a list that the API reads, so each environment needs the manual step below, once:

| Old shared variable | New shared variable |
| --- | --- |
| `ControlPlane__FirstSystemAdminEmail` | `ControlPlane__SystemAdmins__RootEmails__0` |
| `Authentication__Clerk__Issuer` | `Host__Authentication__Issuer` |
| `Authentication__Clerk__AuthorizedParties__0` | `Host__Authentication__AuthorizedParties__0` |
| `Authentication__Clerk__AuthorizedParties__1` | `Host__Authentication__AuthorizedParties__1` |
| `ForwardedHeaders__KnownProxies__<n>` | `Host__ForwardedHeaders__KnownProxies__<n>` |
| `ForwardedHeaders__KnownNetworks__<n>` | `Host__ForwardedHeaders__KnownNetworks__<n>` |

The maintainer does it in `staging` before the pull request of step r5 merges, and in `production` before its first deploy of that release. It is manual, because the values stay out of the repository and only the maintainer changes Railway (the root `AGENTS.md`, "Repository settings"):

1. In the Railway dashboard, open Shared Variables of the environment. For each old variable in the table that exists, add the new variable with the same value, and tick `api-web` and `api-worker` for it ("Configure Shared Variable"). `ControlPlane__SystemAdmins__RootEmails__0` holds the old first system admin's address; add `__1`, `__2` and so on for more root system admins. A `ForwardedHeaders__*` variable exists only if someone set one; `sharedVariables` does not name one, so a new name of it also needs its service variable. The running version ignores the new variables.
2. On the pull request branch, run `pnpm railway:plan`. It must show only the renamed variables of `api-web` and `api-worker` and the two `source` changes (section 7, "Add a shared variable"). Do not apply.
3. Merge, and let the deploy finish. The version before it still needs the old names to start, also after a rollback (section 6).
4. Then untick `api-web` and `api-worker` for each old variable and delete it (section 7, "Remove a shared variable").

## 8. Migrations and the running version

`deploy.yml` runs the migration while the old version still serves. The new version starts after the migration. So every migration must work with the version before it. A migration that breaks the previous version breaks production during the deploy.

Make a breaking change in two releases:

1. Expand: add the new column, table or constraint, and keep the old one. Release the code that writes both and reads the new one.
2. Contract: in a later release, after no running version uses the old one, remove it.

Staging runs each migration before production does, against its own database.

Sources: Humble and Farley, *Continuous Delivery*, chapter 12 ("Managing Data"). Ambler and Sadalage, *Refactoring Databases*, the transition period.

The same is true for messages. During a rolling deploy, the old and the new worker run side by side for a short time. An old worker that takes a message only the new version knows puts it back in the queue, every 30 seconds, up to 10 times (W12 in `apps/api/docs/ARCHITECTURE.md`). A new worker can take it on any of those tries, and takes it at the latest once the old one stops. A message that no worker handles in those 5 minutes goes to the dead letter queue, never to nothing. Wolverine logs each put-back as an error, "was rescheduled to queue". These lines are expected during a deploy. An old worker still runs an old handler for a message it knows, and it does not run a handler that only the new version has.

## 9. The database connections

Each environment has its own PostgreSQL service `postgres`, from Railway's template (section 3). The template creates the database `railway` and the superuser `postgres` (the template's `POSTGRES_DB` and `POSTGRES_USER`). The API never uses that superuser, and no API service references the template's variables `DATABASE_URL`, `PGUSER` or `PGPASSWORD`. The API connects with the two roles of `apps/api/db/bootstrap.sql` (R2 and R7 in `apps/api/docs/ARCHITECTURE.md`).

| Connection | Used by | Route | `SSL Mode` |
| --- | --- | --- | --- |
| `ConnectionStrings__Database` | `api-web` and `api-worker` | The private network of the environment | `Require` |
| `MIGRATIONS_CONNECTION_STRING` | The `migrate` command in `deploy.yml`, on the GitHub runner | The TCP proxy, over the internet | `VerifyCA`, with `MIGRATIONS_ROOT_CERTIFICATE` |

### The server's certificate

- The template's image creates its own certificates when it initialises the volume: a root certificate `CN=root-ca`, and a server certificate `CN=localhost` that the root signs. They are on the volume, in `/var/lib/postgresql/data/certs`.
- The server certificate names `localhost` and the private host of the service, `postgres.railway.internal`. It does not name the host of the TCP proxy.
- No public CA signs it. A client that trusts only public CAs cannot verify it.
- Each volume gets its own root certificate, and that root signs only this server's certificate.
- Both certificates last 820 days (`SSL_CERT_DAYS`). At a restart or a redeploy, the image makes new certificates, with a new root, when the server certificate expires within 30 days.

Source: the repository `railwayapp-templates/postgres-ssl`: `init-ssl.sh`, `wrapper.sh` and `README.md` ("Certificate expiry", "Certificate renewal").

### `ConnectionStrings__Database`

The application role over the private network. It is a sealed Railway shared variable:

```text
Host=postgres.railway.internal;Port=5432;Database=railway;Username=api_application;Password=<application password>;SSL Mode=Require
```

- The host is the service's private name, `<service>.railway.internal` (Railway docs, "Private Networking"). The port is the template's `PGPORT`, 5432.
- The connection is direct, with no pooler. So Wolverine uses it too, and `ConnectionStrings__Messaging` is not set (`apps/api/docs/configuration.md`).
- `SSL Mode=Require`: the connection is encrypted and never falls back to plain text, but the client does not verify the certificate. A verified mode needs the root certificate as a file in the API's container, and the API image has none. A pinned root would also stop the API at each certificate renewal. The private network is an encrypted WireGuard network of this environment only (Railway docs, "How Private Networking Works"), so nobody outside the environment can intercept the connection.

### `MIGRATIONS_CONNECTION_STRING`

The owner role over the TCP proxy, from the GitHub runner. It is a GitHub environment secret:

```text
Host=<proxy domain>;Port=<proxy port>;Database=railway;Username=api_owner;Password=<owner password>;SSL Mode=VerifyCA
```

- Use the owner role `api_owner` that `apps/api/db/bootstrap.sql` creates.
- The proxy domain and port come from section 4, step 7 (Railway docs, "TCP Proxy").
- `SSL Mode=VerifyCA`: the client checks that the server's certificate chains to the environment's own root certificate. `MIGRATIONS_ROOT_CERTIFICATE` holds that root. `deploy.yml` writes it to a file and names the file in `PGSSLROOTCERT`, which Npgsql reads.
- `VerifyFull` does not work here. It also checks the host name, and the certificate does not name the proxy's host. The maintainer chose `VerifyCA` with the pinned root (step 18f). PostgreSQL docs, section 32.19.3, "Protection Provided in Different Modes": with a local CA or a self-signed certificate, `verify-ca` often gives enough protection. Here the root is unique to one server, so a certificate that chains to it comes from that server.
- A test with Npgsql 10 and certificates made like the template's: `VerifyFull` with the right root and a host name that the certificate does not name was rejected. `VerifyCA` with the right root was accepted. `VerifyCA` with another root of the same name was rejected.

Only the `migrate` command gets this connection (R3). It never goes to Railway.

### When the root certificate changes

The template makes a new root certificate about every 27 months (the renewal above). A point-in-time restore creates a new service with its own volume, name and certificates (Railway docs, "Point-in-Time Recovery").

After a renewal, the migration fails with "The remote certificate was rejected". Nothing was deployed. Download the new root certificate (section 4, step 9) and set `MIGRATIONS_ROOT_CERTIFICATE` again (section 4, step 12). The API services keep working, because `Require` does not check the root.

After a restore, the private host, the proxy and the root certificate are new. Update `ConnectionStrings__Database`, `MIGRATIONS_CONNECTION_STRING` and `MIGRATIONS_ROOT_CERTIFICATE`. The restored service has another name, so `.railway/railway.ts` does not declare it, and the next plan can remove or replace a database. Do not apply that plan. Report it, and change the file in a pull request first.

## 10. Deviations and known gaps

- **Staging runs no automated acceptance tests yet.** It runs only the smoke test of `deploy.yml`. *Continuous Delivery*, chapter 5, puts an automated acceptance test stage before a release. The job `e2e` of `ci.yml` runs the Playwright tests on each pull request, but it answers the API in the browser and does not test staging, so it does not close this.
- **Production's backups stay inside Railway.** The volume backups and the archive of point-in-time recovery are in the same Railway project as the database. The 3-2-1 backup rule asks for three copies of the data, on two kinds of media, with one copy off site (Peter Krogh, *The DAM Book*). Planned: a scheduled `pg_dump` of production to storage outside Railway, when production holds real data.
- **Anonymous callers share one rate limit on Railway.** The API limits a caller without a token by its IP address (`apps/api/src/Api/RateLimiting/UserRateLimiting.cs`). Behind Railway's proxy, every caller has the proxy's address, because no `Host:ForwardedHeaders:KnownProxies` or `KnownNetworks` is set. So all anonymous calls to one pod share one limit, 1000 a minute by default. Heavy anonymous traffic can make the smoke test see 429, not 401. The maintainer decides whether to set the forwarded headers settings for Railway. The web apps call the API over the private network, from their own address, so an anonymous call through a BFF shares the limit of that app's replica too. A BFF answers 401 itself without a session (F6), so few anonymous calls reach the API.
- **The npm advisories of three packages have no safe fix today.** `uuid` 8.3.2 and `stream-json` 1.9.1 come through `jayson` 4, which `@solana/web3.js` 1.x requires (`^4.3.0`, also in 1.99.0), and `@clerk/ui` brings in the Solana packages. The fixed versions are new major versions (`uuid` 11.1.1, `stream-json` 3.6.0) that `jayson` 4 does not accept (`^8.3.2`, `^1.9.1`). `braces` 3.0.3 has no fixed release (GHSA-vfj7-8cjw-p6xm). Dependabot cannot update a version-pinned transitive dependency with pnpm (dependabot-core issue 15766), so its run fails until then. Check again when `@solana/web3.js` or `braces` releases. Source: `pnpm audit` and `pnpm why`.
- **A crash outside `Main` can hang the process on arm64.** Step 18g makes `Main` return an exit code. An exception that leaves another thread still calls `abort()`. As PID 1 in a container on arm64, this can hang at full CPU (dotnet/runtime issues 66707 and 111601; 111601 is open for .NET 10). A small init process (`tini`, or `docker run --init`) would reap the process. Planned: decide this in a later step about the image.
- **Errors and uptime are monitored; metrics and traces are not.** Better Stack collects the errors of the three apps and checks their public routes every 3 minutes (section 16). A monitor that fails for 5 minutes emails the owner, and a new error or a spike posts to Slack. Not covered yet: metrics and traces. OpenTelemetry is not connected to a backend: `OTEL_EXPORTER_OTLP_ENDPOINT` is not set (`apps/api/docs/configuration.md`). Planned: step 42. No alert reaches the phone except through Slack's app until production, which brings push, phone and SMS with a responder seat (step 47). Source: *Release It!* (Nygard), "Transparency".

## 11. When a deploy, a tag or a rollback fails

- "The secret ... of the environment ... is empty": set the secret (section 4, step 12).
- "Production already runs a newer commit": the approved run is older than the commit that production runs. Reject it, and approve the newest waiting run. To go back on purpose, use the rollback (section 6).
- The migration fails: the step prints the error of the `migrate` command. Nothing was deployed.
- The migration step stops at its time limit of 5 minutes: the migrations ran longer than that. Nothing was deployed. A database that does not answer fails much sooner, with the error above. A migration that needs more time needs a higher `timeout-minutes` on that step of `deploy.yml`.
- The migration fails with "The remote certificate was rejected": the database has a new root certificate. Set `MIGRATIONS_ROOT_CERTIFICATE` again (section 9, "When the root certificate changes").
- The Railway step fails: the step prints the Railway CLI's own message. Check `RAILWAY_TOKEN` and the service names.
- A deployment ends as `FAILED`, `CRASHED` or `REMOVED`: the step prints its ID. Read its logs in Railway. A missing shared variable or a private GHCR package are common causes.
- "Not healthy after 10 minutes" with "no new deployment yet": Railway started no deployment after the commit. A digest that the newest deployment already has does not cause this: the step then waits for that deployment (section 2).
- "The variable API_URL ... is empty", or `WEB_URL` or `PLATFORM_URL`: set it (section 13, "Custom domains").
- `web` or `platform` is not healthy: Railway's health check calls `GET /health` on `PORT`. A missing `CLERK_PUBLISHABLE_KEY`, `CLERK_SECRET_KEY` or `API_URL` stops the app at start, and its log names the variable (F7).
- The smoke test of a web app fails on `GET /`: each attempt and the error name the host where the redirect chain stopped, and curl's exit code. Exit code 28 is a timeout. `stopped at a third party: <Clerk's Frontend API host>` with exit code 28 means that Clerk's Frontend API did not answer within 15 seconds; the status is then the app's last redirect, such as 307. A path on the Clerk domain with exit code 0 means that the Clerk instance refused the app's domain. `stopped at the app` with exit code 28 means that the app did not answer. Any other path on the app's domain means that the sign-in redirect is wrong. Example:

  ```text
  platform: GET / without a session ended at /v1/client/handshake with 307, curl exit code 28, stopped at a third party: <Clerk's Frontend API host>
  ::error::platform: GET / without a session did not end at https://<platform domain>/sign-in with 200 (last attempt: stopped at a third party: <Clerk's Frontend API host>, curl exit code 28) within 60 seconds.
  ```
- The smoke test fails: the step prints the status codes. A 404 on `/v1/me/tenants` means the URL reaches a process that is not the web role. A 429 means the anonymous rate limit is used up (section 10).
- `tag-release` fails: production runs the new version, but it has no tag yet. Re-run the job. If the tag was created and only the GHCR step failed, the re-run creates the next `N` for the same commit.
- "... is not a release tag vYYYY.0M.0D.N" or "... is not on main": run `rollback.yml` on a release tag that `tag-release` created.
- "Delivery on main did not publish an image for this commit": the tag does not name a commit that `delivery.yml` published.

## 12. A product in a private repository

A product starts from this template with "Use this template". Its repository is often private. Some features of this pipeline then work only on some GitHub plans. This section lists what changes. It designs nothing: a later step does that. The facts were checked on 2026-10-12.

### What each GitHub plan gives a private repository

GitHub Pro is the plan of a personal account. GitHub Team is the plan of an organization. GitHub Free for organizations gives the same as GitHub Free here.

| Feature | Free | Pro | Team | Enterprise Cloud |
| --- | --- | --- | --- | --- |
| Environments and environment secrets | No | Yes | Yes | Yes |
| Required reviewers of an environment | No | No | No | Yes |
| Rulesets | No | Yes | Yes | Yes |
| Artifact attestations | No | No | No | Yes |
| Actions minutes a month | 2,000 | 3,000 | 3,000 | 50,000 |

Sources, GitHub Docs:

- "Managing environments for deployment": environments, environment secrets and deployment branches in a private repository need Pro, Team or Enterprise. On Free, Pro and Team, required reviewers and wait timers work only in public repositories.
- "GitHub's plans": Enterprise Cloud lists "Deployment protection rules with GitHub Actions for private or internal repositories". The same page gives the Actions minutes.
- "About rulesets": rulesets work in private repositories with Pro, Team and Enterprise Cloud.
- "Using artifact attestations to establish provenance for builds": on Free, Pro and Team, only in public repositories. A private repository needs Enterprise Cloud.

Section 4 of `docs/github-settings.md` names the same limits for `pnpm github:apply`.

### What the pipeline needs on GitHub Pro or Team

Pro or Team is the recommended plan for a private product. The pipeline then needs two changes. A later step designs them.

- **The production approval.** Required reviewers are not available, so the `production` environment cannot wait for an approval. Production starts from a manual run of a workflow (`workflow_dispatch`) on `main` instead. Only a person with write access to the repository can start it (GitHub Docs, "Manually running a workflow"). *Continuous Delivery*, chapter 5, calls this a push-button release.
- **The provenance of the image.** Artifact attestations are not available, so `publish-image.yml` cannot attest the images, and `rollback.yml` cannot run `gh attestation verify`. The release tag already names the image digests (section 5), and a release tag cannot be deleted or moved. The rollback check uses those digests instead: each image that it deploys must have the digest that the tag message names.

### What Railway needs

The GHCR package of a private repository is private. Section 5.4 of `docs/github-settings.md` makes the package public. For a private product, that would publish its image.

Railway pulls an image from a private registry only on the Pro plan, with registry credentials. For GHCR, the credential is a personal access token with the scope `read:packages`. The Hobby plan cannot pull a private image (Railway docs, "Private registries").

### The monthly cost

A private product with staging and production, on GitHub Pro and Railway Pro. Prices of 2026-10-12, in USD. Prices change. Check them before a decision.

| Item | Cost a month | Source |
| --- | --- | --- |
| GitHub Pro | 4 | Third-party pricing guides. github.com/pricing lists only Free, Team (4 a user, for the first 12 months) and Enterprise. |
| Railway Pro | 20, with 20 of usage included | railway.com/pricing |
| Railway usage: two environments, each with `api-web`, `api-worker` and `postgres` | About 8, inside the included 20 | See below |
| **Total** | **About 24** | |

- Railway usage: memory costs about 10 a GB a month, a vCPU about 20 a month, and a volume about 0.15 a GB a month (railway.com/pricing). Staging used about 0.35 GB of memory and almost no CPU for its three services on 2026-10-12, without traffic (Railway metrics). Two such environments use about 7 to 8 a month. Traffic adds to this.
- Actions: one merged pull request used about 26 minutes (CI 12, Delivery 14, runs 38290763555 and 38301523555). The 3,000 minutes of Pro cover about 100 merges a month. A minute over that costs 0.006 on a Linux 2-core runner (GitHub Docs, "Actions runner pricing").
- GHCR: the storage and the data transfer of container images are free today. GitHub gives at least one month's notice before this changes (GitHub Docs, "GitHub Packages billing").

### Rejected alternatives

- **Railway builds the image from the source on Hobby.** Each environment then builds its own image, which breaks "Only build your binaries once" (*Continuous Delivery*, chapter 5; Railway docs, "Builds").
- **A VPS with Kamal.** Cheaper: a server with 1 vCPU and 2 GB costs 12 a month at DigitalOcean (digitalocean.com/pricing/droplets). At today's 0.35 GB an environment, it holds both. But the maintainer runs the server, the database and its backups. Kamal starts a database as an accessory, and does not update it when it deploys (Kamal docs, "Accessories").
- **Coolify.** Its repository published 11 critical security advisories on 2026-01-03 and 5 more in June and July 2026 (GitHub Security Advisories of `coollabsio/coolify`). Its control plane needs a server with 2 GB of memory (Coolify docs, "Installation").

## 13. The web apps on Railway

The web app (`apps/web`) and the platform app (`apps/platform`) run on Railway next to the API, in the same environment, each as a service of its own. This section holds what they need outside the repository. In the commands, `<web domain>`, `<platform domain>` and `<api domain>` are the custom domains of the environment. No domain name goes into the repository (section 3).

### The API's settings for the web apps

These shared variables exist already. Their values change to the custom domains. Set them in the Railway dashboard, as in step 3 of section 4:

| Shared variable | Value | Why |
| --- | --- | --- |
| `Host__Authentication__AuthorizedParties__0` | `https://<web domain>` | The API accepts a token only from an origin that this list names (the token's `azp`) |
| `Host__Authentication__AuthorizedParties__1` | `https://<platform domain>` | The same, for the platform app, which runs on an origin of its own |
| `ControlPlane__Invitations__AcceptUrl` | `https://<web domain>/invitations/accept` | The invitation email links to the web app's accept page (F44 in `docs/frontend/ARCHITECTURE.md`) |

One shared variable is new:

| Shared variable | Value | Why |
| --- | --- | --- |
| `CLERK_PUBLISHABLE_KEY` | The publishable key of the environment's Clerk instance (`pk_test_...` or `pk_live_...`) | Both apps read it at run time (F7). It is not a secret, so it is a plain variable, not a sealed one |

The apps read the Clerk secret key from `ControlPlane__Clerk__SecretKey`, which the API reads too. So the secret key has one source (`.railway/railway.ts`).

### Clerk

The staging environment uses the Clerk development instance. A development instance accepts sign-in on any domain: Clerk's docs tell you to use "your host's provided preview domain ... with development API keys" (Clerk docs, "Instances / Environments", "Use your host's provided preview domain"). Its session state travels in the query (`__clerk_db_jwt`) through a "handshake" with its Frontend API on `accounts.dev`, not in cookies of a shared domain (the same page, "Session architecture differences"). The instance setting `allowedOrigins` is for browser extensions, Electron and Capacitor only (Clerk Backend API, "Update instance settings"). So `.clerk/config.json` does not change, and `pnpm clerk:apply` is not part of the manual steps. The smoke test of `deploy.yml` follows Clerk's handshake on each deploy, so it shows when the instance does not accept a domain.

A production instance needs its own domain with DNS records for Clerk's Frontend API (Clerk docs, "Deploy your app to production"). This document does not cover it yet: production is set up later (section 4).

### The private network

The apps call the API at `http://<api-web private domain>:8080`. `.railway/railway.ts` writes it as a Railway reference, `http://${{api-web.RAILWAY_PRIVATE_DOMAIN}}:8080`, so no host name is a value in the repository.

- Plain `http`: private traffic runs through WireGuard tunnels, and Railway's docs say to use `http://` inside the network (Railway docs, "How Private Networking Works").
- An environment created after 16 October 2025 resolves a private name to an IPv4 and an IPv6 address; an older one to IPv6 only (the same page, "DNS resolution"). The project of this repository was created after that date, so both environments resolve to both.
- The API listens on port 8080 on all interfaces (the base image sets `ASPNETCORE_HTTP_PORTS=8080`, and `apps/api/Dockerfile` says so), so it answers on IPv4 and IPv6.
- `api-web` keeps its public domain for the later mobile app.

### Custom domains

Each environment has its own domains: `<api domain>` for `api-web`, `<web domain>` for `web` and `<platform domain>` for `platform`. They are Railway service settings, not part of `.railway/railway.ts`, because no domain name goes into the repository. For each of the three services, in the Railway dashboard of the environment:

1. Open the service, Settings, Networking, "Custom Domain", and enter the domain.
2. Choose the target port: 8080 for `api-web`, 3000 for `web` and `platform` (`.railway/web.json` and `.railway/platform.json`). Never 8081 for `api-web`.
3. Railway shows a `CNAME` record and a `TXT` record. Create both at the DNS provider, exactly as shown. Without the `TXT` record, Railway does not verify the domain, and the domain answers 404 (Railway docs, "Working with Domains").
4. Wait for the green check mark next to the domain. Railway then issues the certificate.

Then store the URLs in the GitHub environment:

```bash
gh variable set API_URL --env <environment> --body https://<api domain>
gh variable set WEB_URL --env <environment> --body https://<web domain>
gh variable set PLATFORM_URL --env <environment> --body https://<platform domain>
```

A later plan of `.railway/railway.ts` may show a custom domain going away, as it shows `source` (section 7). Do not apply such a plan. Report it.

### Add the web apps to a running environment

Staging runs the API already. Do these steps in this order, once, for `staging` only. Production waits (section 7). Each step is manual because it sets a value that stays out of the repository, or because only the maintainer may run it (the root `AGENTS.md`, "Repository settings").

Before step 1, set the shared variables in the Railway dashboard, Shared Variables of `staging`: add `CLERK_PUBLISHABLE_KEY`, and change the values of `Host__Authentication__AuthorizedParties__0`, `Host__Authentication__AuthorizedParties__1` and `ControlPlane__Invitations__AcceptUrl` (section 13, "The API's settings for the web apps"). Why first: a reference to a shared variable that does not exist gives an empty value (section 7). Manual: values never go into the repository. The running API reads the new values only at its next deploy.

1. **Read the plan.** On the pull request branch, with the CLI linked to `staging`:

   ```bash
   railway link
   pnpm railway:plan
   ```

   Apply only if the plan shows exactly these lines: create `web` and `platform`; `source.image` and `source.type` of `api-web` and `api-worker` going to `null` (section 7); "Delete variable" `Host__Cors__AllowedOrigins__0` on `api-web` and `api-worker`. Any other line: stop, and report it. Manual: only the maintainer plans and applies (the root `AGENTS.md`).
2. **Apply.** The plan has destructive lines, so the command needs `--confirm-destructive` (section 7). Add it only after step 1:

   ```bash
   pnpm railway:apply --confirm-destructive
   ```

   Then look at the image of `api-web` and `api-worker` in the Railway dashboard: removed or kept. In staging on 2026-10-19, with Railway CLI 5.64.2, the apply kept it: Railway redeployed both services with the same image, because their variables changed, and both came online (section 7).
3. **Give the API its image back at once.** Rerun the last successful Delivery run of `main`, before the domains and the DNS wait. Its `deploy.yml` sets the API's image again by digest. Without the CORS variable, the API of `main` still starts, because `Host:Cors:AllowedOrigins` is optional there.

   ```bash
   gh run list --workflow Delivery --branch main --status success --limit 1
   ```

   ```bash
   gh run rerun <run id>
   ```

   If step 2 kept the image, skip the rerun: the API still runs. A rerun is safe then too: `deploy.yml` sees that the newest deployment already has the digest and waits for it (section 2). In staging on 2026-10-19 the apply kept the image, so this rerun was skipped (section 7). Manual: the maintainer decides when a deploy starts.
4. **Add the custom domains, their DNS records and the GitHub environment variables** for `api-web`, `web` and `platform` (section 13, "Custom domains"). Manual: the domains and the DNS stay out of the repository. A domain verifies only after its `TXT` record resolves, which can take minutes.
5. **Switch the staging deploy off before the merge.**

   ```bash
   gh variable set DEPLOY_STAGING --body false
   ```

   Why: the merge pushes the images `web` and `platform` for the first time, and a new GHCR package is private. Railway pulls without credentials, so the deploy of the merge would fail on them.
6. **Merge the pull request.** Delivery publishes the three images. `deploy-staging` is skipped.
7. **Make the GHCR packages `web` and `platform` public** (`docs/github-settings.md`, section 5.4). Manual: the visibility is a setting on the package's page, and a public package cannot become private again.
8. **Switch the staging deploy on and rerun the merge's Delivery run.** Its `deploy.yml` deploys all three images: it migrates, deploys the API, then `web` and `platform`, and smoke-tests the three.

   ```bash
   gh variable set DEPLOY_STAGING --body true
   ```

   ```bash
   gh run list --workflow Delivery --branch main --limit 1
   ```

   ```bash
   gh run rerun <run id>
   ```

   The first deploy is green when the job "Deploy to staging" passes.
9. **Delete the old shared variable.** In the Railway dashboard, Shared Variables of `staging`: delete `Host__Cors__AllowedOrigins__0`. No service references it after step 2 (section 7, "Remove a shared variable"). Manual: the dashboard holds the shared variables.

`pnpm clerk:apply` is not a step: `.clerk/config.json` does not change (section 13, "Clerk").

## 14. Retry a failed message

A message that fails all its retries goes to the dead letter queue: the table `wolverine.wolverine_dead_letters` in the environment's database (W11 in `apps/api/docs/ARCHITECTURE.md`). Wolverine keeps it for `Host:Messaging:DeadLetters:Retention`, 14 days by default. Then a worker deletes it (W13). The API has no endpoint for dead letters.

Retry a dead letter only after its cause is fixed. Examples: the email service was down for longer than the retries, or a setting was wrong and is now correct. A message whose cause is still there fails its retries again and comes back to the table.

An invitation email does not need this step. The inviter can invite the same address again: the new invitation replaces the old one and sends a new email (K2 in `apps/api/docs/api-catalog.md`).

1. **List the dead letters.** Connect to the database as `api_owner`. The query leaves out `body`, because the body holds the message, email addresses and invitation links included.

   ```sql
   SELECT id, message_type, exception_type, exception_message, sent_at, expires
   FROM wolverine.wolverine_dead_letters
   ORDER BY sent_at DESC;
   ```

2. **Mark one dead letter for a retry.** Use the `id` from step 1.

   ```sql
   UPDATE wolverine.wolverine_dead_letters
   SET replayable = true
   WHERE id = '<id>';
   ```

3. **Wait for a worker.** A worker's durability agent moves each row with `replayable = true` back to the incoming messages and handles it again. It polls, so this takes a few seconds. The row then leaves `wolverine_dead_letters`. If the message fails its retries again, it comes back with a new expiry.

Source: Wolverine, "Dead Letter Storage" (the `replayable` column and the durability agent's replay) and "Dead Letter Expiration". Hohpe and Woolf, *Enterprise Integration Patterns*, "Dead Letter Channel".

## 15. Reset the staging database

A pull request that squashes the migrations replaces each module's migrations with one initial migration. The staging database still has the old migrations in its history, so the `migrate` step of the new version fails there: the history holds no initial migration, so EF Core runs it, and its first `CREATE TABLE` fails on a table that exists. The reset removes everything the migrations created. The next delivery then creates it again from the initial migrations.

Run it only when all of these are true:

- No release exists yet. Production has no data to keep.
- The environment is staging. Never run it on production.
- A pull request that squashes the migrations waits for its merge, and its description asks for the reset.

What the reset keeps: the roles `api_owner` and `api_application` with their passwords, the database `railway`, the extension `pg_trgm` and the identity provider's users. The identity provider also keeps the sign-up invitations of the pending invitations (`invitations.sign_up_invitation_id`); their links lead to an invitation that no longer exists, until each expires at the provider. What it removes: the schemas `api`, `audit`, `control_plane` and `wolverine`, with every tenant, user row, invitation, audit entry, idempotency key and message in them, scheduled messages included.

Use the values of staging only: its proxy domain and port (section 4, step 7) and its root certificate (section 4, step 9). The commands are the ones of production too, so check each value before you run them. Run them from the repository root.

1. **Read the password of `api_owner` of staging.** It is in the password manager (section 4, step 2). `api_owner` owns the four schemas, so it can drop them, and it needs no superuser.

   ```bash
   printf 'Password of api_owner in staging: '; read -rs PGPASSWORD; echo
   export PGPASSWORD
   ```

2. **List the schemas that `api_owner` owns.** Replace `<proxy domain>` and `<proxy port>` with the values of staging:

   ```bash
   docker run --rm -e PGPASSWORD \
     -v "$HOME/<project>-keys/staging-database-root.crt:/root.crt:ro" \
     postgres:18 psql "host=<proxy domain> port=<proxy port> dbname=railway user=api_owner sslmode=verify-ca sslrootcert=/root.crt" \
     -At -c "select nspname from pg_namespace where nspowner = 'api_owner'::regrole order by nspname"
   ```

   The expected output is four lines: `api`, `audit`, `control_plane` and `wolverine`. If the output holds any other schema, or misses one, stop. Find the cause before the next step.

3. **Drop the four schemas.** One transaction drops all four, or none: `ON_ERROR_STOP` stops `psql` at the first error, and the transaction then rolls back:

   ```bash
   docker run --rm -i -e PGPASSWORD \
     -v "$HOME/<project>-keys/staging-database-root.crt:/root.crt:ro" \
     postgres:18 psql "host=<proxy domain> port=<proxy port> dbname=railway user=api_owner sslmode=verify-ca sslrootcert=/root.crt" \
     -v ON_ERROR_STOP=1 <<'SQL'
   begin;
   drop schema api, audit, control_plane, wolverine cascade;
   commit;
   SQL
   ```

   `psql` prints `NOTICE:  drop cascades to ... other objects`, the list of the tables, and then `COMMIT`.

4. **Run the check of step 2 again.** It prints nothing.

5. **Unset the password:**

   ```bash
   unset PGPASSWORD
   ```

6. **Merge the pull request at once.** The delivery to staging runs the `migrate` step, which creates the four schemas from the initial migrations, and then deploys the new version. Until then, staging answers errors: the running version has no tables. If another pull request merges first, its delivery creates the old migrations again, and the squash's `migrate` step then fails: run steps 1 to 5 again, then merge.

After the delivery, check:

- The `migrate` step of the staging deploy logs one migration for each module: `Migrated the control_plane schema: 1 migrations applied`, the same for `audit` and `api`, and `Migration finished`.
- The staging deploy of `deploy.yml` succeeds: Railway's health check passes for `api-web` and `api-worker`. The web apps load.
- A person on `ControlPlane:SystemAdmins:RootEmails` signs in to the platform app with a second factor and becomes a system admin again: the door adds the user row (`apps/api/docs/flows/system-admins.md`). No other user row, tenant or invitation is left: create the test tenants again, and invite their members again.

A rollback of staging to a version from before the squash does not work after the reset (section 6, "The database").

## 16. Error tracking and alerts

Better Stack collects the errors of the API, the web app and the platform app, checks their public routes, waits for the worker's heartbeat and alerts the owner. The three apps send their errors through the Sentry SDK. `.betterstack/config.json` holds the settings that Better Stack's API can manage, and `.betterstack/README.md` says which ones it cannot and why. `scripts/betterstack/config.sh` plans and applies them, like Railway's and Clerk's settings (section 7).

What the file holds:

| Setting | Value | Why |
| --- | --- | --- |
| Errors applications | `api`, `web`, `platform`, one per app, for every environment | The `environment` tag of each event separates staging and production: the API's `ASPNETCORE_ENVIRONMENT` in lower case, and the web apps' `ERROR_TRACKING_ENVIRONMENT` |
| Monitor `<environment> api` | `GET <API_URL>/v1/me/tenants` must answer 401 | The public route of the smoke test (section 2, step 6). The health endpoints answer only on the management port |
| Monitors `<environment> web` and `<environment> platform` | `GET <WEB_URL>/health` and `GET <PLATFORM_URL>/health` must answer 2xx | The health paths of `.railway/web.json` and `.railway/platform.json` (section 2, step 9) |
| Heartbeat `<environment> api-worker` | Expects a call every 180 seconds (`period`), with 120 seconds of grace (`grace`) | `api-worker` answers no public route, so no monitor can call it. The worker calls the heartbeat's URL with GET every 60 seconds while it is ready. A lost call never alerts: the period holds three calls. A crash or a hang alerts about 5 minutes after the last call (180 + 120 seconds), as the monitors do |
| Check interval | 180 seconds | The free plan's shortest interval |
| Alert delay | 300 seconds of failure; resolved after 180 seconds up | A deploy's restart or a short network fault needs no person. Google, *Site Reliability Engineering*, chapter 6: alert on symptoms, and only when a person must act |
| Alert channel | Email, no escalation policy | The free plan has email and Slack alerts only. Push, phone and SMS need a responder seat (step 47) |

`plan` and `apply` compare only the monitors and heartbeats of one environment: those whose name starts with `<environment> `. `apply` deletes such a monitor or heartbeat when the file does not hold it. A heartbeat's URL holds its token, so the script never prints it. It never deletes an Errors application: deleting one deletes its errors, so `plan` prints that it leaves it.

### First setup

Do these steps once for staging. For production, do steps 3 to 7 with `production` and the production URLs; the Errors applications and the Errors integrations exist then.

1. **Create the Better Stack account** on the free plan, with the owner's email address. The account makes a team. Keep the owner the only member of the team: the monitors alert the team's members by email.
2. **Create two team-based API tokens.** In Better Stack, open "API tokens", then "Team-based tokens", and pick the team. Create one token under "Incidents API tokens" and one under "Telemetry API tokens". Keep both in the password manager. They never go into a file, a GitHub secret or Railway. Better Stack has no read-only token, so `plan` uses the same tokens as `apply`.
3. **Read the tokens and the URLs into the shell.** The URLs are the GitHub environment variables that `deploy.yml` reads (section 3). Run from the repository root:

   ```bash
   printf 'Incidents API token: '; read -rs BETTERSTACK_UPTIME_TOKEN; echo
   printf 'Telemetry API token: '; read -rs BETTERSTACK_TELEMETRY_TOKEN; echo
   export BETTERSTACK_UPTIME_TOKEN BETTERSTACK_TELEMETRY_TOKEN
   export API_URL="$(gh variable get API_URL --env <environment>)"
   export WEB_URL="$(gh variable get WEB_URL --env <environment>)"
   export PLATFORM_URL="$(gh variable get PLATFORM_URL --env <environment>)"
   ```

4. **Plan, read the plan, then apply:**

   ```bash
   pnpm betterstack:plan <environment>
   pnpm betterstack:apply <environment>
   ```

   The first plan for staging creates the three Errors applications, the three monitors and the heartbeat `staging api-worker`. `apply` asks before it changes anything. Run the plan again: it prints `No difference.`
5. **Set the DSN of each application in Railway.** In Better Stack, open Errors, then each application, then its "Ingest" tab. The DSN is `https://<application token>@<ingesting host>/<application id>` (Better Stack docs, "Sentry SDK"). In the Railway dashboard, set these shared variables of `<environment>`, the DSNs as sealed variables. Never paste a DSN into a file or a chat.

   | Shared variable | Value |
   | --- | --- |
   | `Host__ErrorTracking__Dsn` | the DSN of the application `api` |
   | `WEB_ERROR_TRACKING_DSN` | the DSN of the application `web` |
   | `PLATFORM_ERROR_TRACKING_DSN` | the DSN of the application `platform` |
   | `Host__Heartbeat__Url` | the URL of the heartbeat `<environment> api-worker`: in Better Stack, open Uptime, then "Heartbeats", then the heartbeat, and copy its URL. It is a secret: its token is in the path |

   `.railway/railway.ts` gives each service its own variable, and `Host__Heartbeat__Url` to `api-worker` only. Without `Host__Heartbeat__Url`, the worker sends no heartbeat, and the heartbeat stays "Pending" and never alerts. It also sets the environment tag of the events from the linked Railway environment: `ASPNETCORE_ENVIRONMENT` for the API, `ERROR_TRACKING_ENVIRONMENT` for the web apps. You set neither. Then run `pnpm railway:plan` and `pnpm railway:apply` for the environment (section 7).
6. **Set the Errors integrations (manual).** Better Stack's API cannot manage the Errors integrations (`.betterstack/README.md`). Two integrations alert: "Incidents", which is on by default and notifies by email, and "Slack".
   1. Choose a Slack channel for the alerts, such as `#alerts-<product>`.
   2. In Better Stack, open Errors, then "Integrations". Find "Slack" and click "Add".
   3. Follow the steps to authorize your Slack workspace, and pick the channel.
   4. Give "Incidents" and "Slack" the same settings, then click "Save changes" on each:

      | Setting | Value |
      | --- | --- |
      | "Applications" | Any application |
      | "Notify me when": "Application has a spike in errors" | On |
      | "Notify me when": "New error occurs" | On |
      | "Notify me when": "Error is automatically re-opened" | On |
      | "Notify me when": "Error reaches an occurrence milestone" | Off: a spike covers it, and a milestone repeats for the same error |
      | "Notify me when": "Error occurred N times within an interval" | Off: a spike covers it |
      | "Only when tags match" | Empty for staging |

      "Slack" has more settings. Set them too:

      | Setting | Value |
      | --- | --- |
      | "Start an AI SRE conversation when Better Stack is tagged" | Off: a change to the repository goes through our own steps, and error data goes to no other AI |
      | "Spike protection starts sampling exceptions" | On |
      | "New release is observed" | Off: the channel holds only what needs action |
      | "How we post updates" | Thread-based |
      | "Send important updates also to #&lt;channel&gt;", under "Thread-based" | On: an important update does not stay hidden in a thread. Better Stack shows the channel of sub-step 3 in the name |

      "Error reaches an occurrence milestone" stays off and "Error is automatically re-opened" stays on, as in the table above. Keep the other settings at their defaults. If Better Stack changes a name, write the new name into this step in a pull request.
   5. Install Slack's mobile app and allow its notifications for the channel. This is the only alert on the phone until step 47.
7. **Remove the tokens from the shell:**

   ```bash
   unset BETTERSTACK_UPTIME_TOKEN BETTERSTACK_TELEMETRY_TOKEN
   ```

After the next deploy, check:

- In Better Stack, Uptime, the three monitors of the environment show "Up".
- In Better Stack, Uptime, "Heartbeats", the heartbeat `<environment> api-worker` shows "Up". "Pending" means that no call came yet: check that `Host__Heartbeat__Url` is set and that `api-worker` is ready.
- An error in an app shows in its Errors application with the right `environment`, and a new error posts to the Slack channel.

### When the plan prints something it cannot apply

- `! Errors application "<name>" has platform ...`: the API cannot change an application's platform. Create a new application with the file's platform by hand, move the DSN in Railway to it (step 5), then delete the old one. Deleting it deletes its errors.
- `! Errors application "<name>" is not in .betterstack/config.json`: the script leaves it. Add it to the file, or delete it by hand when its errors are not needed.
- A monitor of another environment, or with a name that does not start with `<environment> `, is never in a plan. Uptime's free plan holds 10 monitors and 10 heartbeats; the two environments use 6 monitors and 2 heartbeats.

### Later steps

- **Push, phone and SMS alerts** come with production (step 47), with a responder seat and an escalation policy in `.betterstack/config.json`.
- **Errors alerts by environment** come with production (step 47). The Errors integration that calls or pushes gets "Only when tags match" set to the tag `environment` matching `production`, so a staging error never pages the owner.
- **A status page** is a later step (1.1).
