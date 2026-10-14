# Provider setup

What the API needs from the providers it runs on, and how to set them up. The architecture does not depend on these providers (fixed rule 6 of `ARCHITECTURE.md`); the notes for each one are here. `configuration.md` lists the settings, and `local-run.md` runs the API on a local machine.

## Database provider

The API needs PostgreSQL 18 and the two roles of R2 and R7: an owner role for the migrations and an application role for running code. `db/bootstrap.sql` creates both.

- `db/bootstrap.sql` runs before the first migration, against the API's own database. It runs as the owner of that database, and that role needs `CREATEROLE`. A superuser is not needed, but a superuser works too.
- The script is safe to run again. A rerun sets the passwords given and changes nothing else. If either role is a superuser, can bypass RLS, can create databases or can create roles, the script stops and names the role and the attribute.
- `ConnectionStrings:Messaging` is needed when `ConnectionStrings:Database` goes through a pooler in transaction mode. Wolverine's message store holds session-level advisory locks, so the setting then names a direct or session-mode connection of the application role.
- `ConnectionStrings:Migrations` names a direct connection of the owner role. Only the `migrate` command gets it (R3).

### Railway

The deployed API uses Railway's PostgreSQL template, one service per environment. `docs/deployment.md` at the repository root gives the steps and the connection formats.

- The template runs the image `ghcr.io/railwayapp-templates/postgres-ssl:18`, with a volume. It creates the database `railway` and the superuser `postgres`.
- Run `db/bootstrap.sql` as `postgres`, once per environment, with new passwords. The API never connects as `postgres`.
- There is no pooler. `ConnectionStrings:Database` connects directly over the private network, so Wolverine uses it too. Do not set `ConnectionStrings:Messaging`.
- Support scope: Railway's database support covers the template as Railway ships it. A database whose roles are changed through SQL leaves that scope, and `db/bootstrap.sql` creates roles through SQL. From then on, the database is ours to manage. Railway's support still helps with the platform: provisioning, volumes, networking, and the flows for backups and point-in-time recovery. Source: Railway docs, "Databases", "Database support scope".

### Neon (alternative)

Neon works too, because the API uses plain PostgreSQL. It is not the default, because the worker polls its PostgreSQL queue every second (`src/Api/Messaging/MessageStorage.cs`), so a Neon compute never suspends.

- Run `db/bootstrap.sql` as the owner role that Neon created with the project, such as `neondb_owner`.
- Create the application role with this SQL, not in the Neon console. A role that the console creates is a member of `neon_superuser`, which can bypass RLS. R10 then stops the application.
- Give each environment its own Neon project or branch. A Neon branch starts as a copy of its parent, with the parent's data and role passwords. So do not branch staging from a production branch that holds data, and run `db/bootstrap.sql` on the new branch with new passwords.
- Neon's pooled endpoint is a pooler in transaction mode. If `ConnectionStrings:Database` uses it, set `ConnectionStrings:Messaging` to the direct endpoint. `ConnectionStrings:Migrations` uses the direct endpoint too: its host name has no `-pooler`.

## Identity provider

The API needs these from an identity provider:

- OpenID Connect discovery (`/.well-known/openid-configuration`) and its signing keys, served over HTTPS with a certificate the server trusts. The issuer URL is `Authentication:Clerk:Issuer`.
- Session tokens that carry `sub` (the user) and `sid` (the session). A token without `sid` is refused (API2).
- `fva` in the session token for the second factor: the minutes since the first and the second factor were verified, `-1` for never. The system door requires a verified second factor (A6).
- Open sign-up with email verification at sign-up. The Owner of a new tenant signs up like any other user and then accepts the invitation with the invited address verified.
- A Backend API that returns a user's verified email addresses and profile. The API calls it when a user accepts an invitation and when the first system admin enters the system door. Its URL and secret key are `ControlPlane:Clerk:BackendApiUrl` and `ControlPlane:Clerk:SecretKey`.
- Webhooks for user changes, signed in the Svix format. The web process receives them at `POST /v1/webhooks/clerk`. The signing secret is `ControlPlane:Clerk:WebhookSigningSecret`.

On Clerk:

- Use the session token. A JWT template's token carries no `sid`, so the API refuses it.
- Enable the second factor. The first system admin enrolls a device before their first request to `/v1/admin/...`.
- Keep sign-up open and require email verification at sign-up.
- The Backend API call is `GET users/{user id}`; the API reads the addresses whose verification status is `verified`. It copies the address marked primary (`primary_email_address_id`), `first_name`, `last_name` and `updated_at`.
- Add a webhook endpoint in the Clerk dashboard for each instance, with the URL `https://<API domain>/v1/webhooks/clerk` and the events `user.updated` and `user.deleted`. Its signing secret starts with `whsec_`.

## Email and tenant creation timing

- `ControlPlane:InvitationEmailTimeout` must be longer than the `Notifications:InvitationEmailRetryDelays` together. Otherwise the tenant creation gives up on an email that is still being tried, cancels the invitation and raises the alarm.
- Outside Development the API does not start without `Notifications:Resend:ApiKey` and `Notifications:Resend:From`. The sender's domain must be verified at Resend.

## Container platform

- The process has two ports (section 8 of `ARCHITECTURE.md`). The public port, 8080, serves the API. The management port, 8081 (`Host:ManagementPort`), serves only `/health/live` and `/health/ready`. A worker opens only the management port. Route the platform's traffic to 8080 only.
- Railway: `docs/deployment.md` at the repository root describes the deployment. `.railway/railway.ts` declares the PostgreSQL service and sets the health check and the variables. `PORT` is 8081, because Railway's health check uses `PORT`. The domain of `api-web` targets 8080.
- The image `apps/api/Dockerfile` has no `HEALTHCHECK`: it has no shell and no `curl` (see the deviations table of `ARCHITECTURE.md`). Set the platform's HTTP health check instead, on port 8081. The readiness path is `/health/ready` and the liveness path is `/health/live`.
- Kubernetes: use `/health/ready` for the readiness probe and `/health/live` for the liveness probe, both on port 8081. The Service exposes only port 8080.
- Set `runAsNonRoot: true`. Both images run as a numeric non-root user.
