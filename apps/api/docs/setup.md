# Provider setup

What the API needs from the providers it runs on, and how to set them up. The architecture does not depend on these providers (fixed rule 6 of `ARCHITECTURE.md`); the notes for each one are here. `configuration.md` lists the settings, and `local-run.md` runs the API on a local machine.

## Database provider

The API needs PostgreSQL 18 and the two roles of R2 and R7: an owner role for the migrations and an application role for running code. `db/bootstrap.sql` creates both.

- `db/bootstrap.sql` runs before the first migration, against the API's own database. It runs as the owner of that database, and that role needs `CREATEROLE`. A superuser is not needed, but a superuser works too.
- The script is safe to run again. A rerun sets the passwords given and changes nothing else. If either role is a superuser, can bypass RLS, can create databases or can create roles, the script stops and names the role and the attribute.
- `ConnectionStrings:Messaging` is needed when `ConnectionStrings:Database` goes through a pooler in transaction mode. Wolverine's message store holds session-level advisory locks, so the setting then names a direct or session-mode connection of the application role.
- `ConnectionStrings:Migrations` names a direct connection of the owner role. Only the `migrate` command gets it (R3).
- A ControlPlane migration creates the extension `pg_trgm` for the search of the lists (K5 in `api-catalog.md`). It is a trusted extension, so the owner role creates it with the `CREATE` privilege on the database that `db/bootstrap.sql` grants. No other grant is needed. The provider's PostgreSQL must offer the standard extensions (`contrib`); the official image, Railway and Neon do.

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
- Invite-only sign-up with email verification. Only an invitation of the identity provider lets a person sign up. The API asks for one when it invites an address that has no account (A7). The person signs up through its link and then accepts our invitation with the invited address verified.
- A Backend API, called only from the API's server. Its URL and secret key are `ControlPlane:Clerk:BackendApiUrl` and `ControlPlane:Clerk:SecretKey`. The API uses four calls and nothing else: let an address sign up, withdraw that permission, read a user's verified email addresses, and delete a user (`IIdentityProvider`).
- No webhooks. Nobody edits a user at the identity provider, so nothing flows back.

On Clerk:

- Use the session token. A JWT template's token carries no `sid`, so the API refuses it.
- Enable the second factor. The first system admin enrolls a device before their first request to `/v1/platform/...`.
- The sign-up mode is Restricted (`.clerk/`, below). Clerk sends no email: the API's invitation email carries Clerk's link.
- Let an address sign up: the API first asks `GET users?email_address=<address>`. An address with an account needs no invitation: Clerk would refuse one, and the person signs in. For any other address the API calls `POST invitations` with `notify` false, `ignore_existing` true, `redirect_url` our accept link with our invitation code, and `expires_in_days` our invitation's lifetime in whole days, rounded up (1 to 365). `ignore_existing` lets two tenants invite the same person, and lets a new invitation replace one whose withdrawal has not run yet. The answer's `url` is the link in our email, and its `id` is kept on our invitation.
- When the person opens the link, Clerk sends them to our accept link and adds `__clerk_ticket`. The web signs them up with the ticket (`signUp.ticket()`), which verifies the invited address. Clerk's own sign-up form shows Google and Microsoft only when the ticket leaves a requirement open; with no password and no names, the ticket alone completes the sign-up. The person then signs in with an email code, Google or Microsoft.
- Withdraw: `POST invitations/{id}/revoke`. An invitation that is no longer pending answers 400, and an unknown one 404; the API takes both as done. Our invitation ends Clerk's when it is revoked, replaced or cancelled, when its tenant closes, and when its lifetime is over; an accepted one needs nothing.
- Read the verified addresses: `GET users/{user id}`; the API reads the addresses whose verification status is `verified`. It reads no name: Clerk keeps none, and the catalog owns the names (A7).
- Delete a user: `DELETE users/{user id}`, after the catalog's deletion commits. A user already gone answers 404, which is done.
- Each call has the time limit `ControlPlane:Clerk:Timeout` and the retries `ControlPlane:Clerk:RetryDelays`.

### Clerk settings as code

The Clerk instance settings are code, as the Railway settings are. One file, `.clerk/config.json` at the repository root, holds the settings of both instances. It holds only the settings below, as a partial instance config (`clerk config schema` lists the keys). It is the one source of the values. Change a setting in the file, in a pull request. The maintainer then applies it to each instance. Nobody changes a setting in the Clerk dashboard, except for the manual steps below.

| Instance | Serves |
| --- | --- |
| development | local and staging |
| production | production. `clerk deploy` creates it (see the manual steps below) |

The two instances do not differ today, so the file has no override for one instance. A change that needs a difference adds the override. Each Clerk application has these two instances. The repository names no application: each maintainer links their own checkout to their own application.

1. `clerk auth login` (or set `CLERK_PLATFORM_API_KEY`).
2. `clerk link`, from the repository root. It asks for the application and stores the link in the CLI's own settings, not in the repository, as `railway link` does. Instead of a link, `CLERK_APP_ID` can name the application, for example in CI.
3. `pnpm clerk:plan` prints, for each instance, the difference between the file and the live instance. It changes nothing. Add `development` or `production` to plan one instance only.
4. `pnpm clerk:apply development` or `pnpm clerk:apply production` prints the same difference for that instance, asks, then applies it. It needs the instance name. Read the plan first.

Without a link and without `CLERK_APP_ID`, the script stops and says so.

The settings. The values are in `.clerk/config.json` only.

| Setting | Key | Why it is set |
| --- | --- | --- |
| Sign-up mode | `auth_access_control.sign_up_mode` | Only an invitation lets a person sign up (A7) |
| Email sign-in and verification | `auth_email.used_for_sign_in`, `auth_email.sign_in_strategies`, `auth_email.verify_at_sign_up`, `auth_email.verification_strategies` | A person signs in with an email code and verifies their address at sign-up. No magic link |
| Password | `auth_password.enabled`, `auth_password.required` | No password: an email code, Google or Microsoft signs a person in |
| Social sign-in | `connection_oauth_google`, `connection_oauth_microsoft` | Google and Microsoft sign a person in. A new person still needs an invitation |
| First and last name | `user_model.first_name`, `user_model.last_name` | Clerk keeps no names. The API keeps them |
| Multi-factor | `auth_multi_factor.authenticator_app`, `auth_multi_factor.backup_code` | The system door needs a verified second factor (`fva`). A backup code signs in a person who lost their device |
| Bot protection on sign-up | `auth_attack_protection.bot_protection.captcha_enabled` | Stops automated sign-up attempts |
| User enumeration protection | `auth_attack_protection.enumeration_protection` | Sign-in never tells a visitor whether an address has an account or an invitation |

Secrets are not in the file. The CLI gets its credentials from `clerk auth login` or `CLERK_PLATFORM_API_KEY`; it does not need the instance secret key. The development instance uses Clerk's shared Google and Microsoft credentials. An apply to production reads the OAuth credentials from `CLERK_GOOGLE_CLIENT_ID`, `CLERK_GOOGLE_CLIENT_SECRET`, `CLERK_MICROSOFT_CLIENT_ID` and `CLERK_MICROSOFT_CLIENT_SECRET`, and stops if one is missing. The script never prints them.

The instance config has no key for these. The maintainer does them by hand:

- Create the production instance: `clerk deploy`, in a terminal. It needs the production domain.
- Create the OAuth applications for production: a Google OAuth client in Google Cloud Console and an app registration in Microsoft Entra ID. Their redirect URIs are in the Clerk dashboard, Configure, SSO connections, under each provider.
- Webhooks: none. The API has no webhook endpoint. Remove any webhook endpoint in the Clerk dashboard, Configure, Webhooks.
- `fva` in the session token: Clerk's session token version 2 carries it. Check it in the Clerk dashboard, Configure, Sessions, and do not remove it in "Customize session token".

The first system admin: in Restricted mode nobody can sign up without an invitation. The maintainer creates the first system admin's account with the address in `ControlPlane:FirstSystemAdminEmail`, in the Clerk dashboard (Users, Create user) or with `clerk users create --instance <dev or prod> --email <address>` in the linked checkout. The account has no password: the admin signs in with an email code and then enrolls an authenticator app. Until that account exists, nobody passes the system door: every `/v1/platform/...` request answers 403, and the staff list stays empty. The account's first request with a verified second factor writes the catalog user, with the configured address and no names, and the grant (section 6). A system admin then gives them their names (`PATCH /v1/platform/users/{userId}`).

## Email and tenant creation timing

- `ControlPlane:InvitationEmailTimeout` must be longer than the `ControlPlane:Clerk:RetryDelays` and the `Notifications:InvitationEmailRetryDelays` together: step 3 lets the owner sign up with Clerk, then sends the email. Otherwise the tenant creation gives up on a call that is still being tried, cancels the invitation and raises the alarm.
- Outside Development the API does not start without `Notifications:Resend:ApiKey` and `Notifications:Resend:From`. The sender's domain must be verified at Resend.

## Container platform

- The process has two ports (section 8 of `ARCHITECTURE.md`). The public port, 8080, serves the API. The management port, 8081 (`Host:ManagementPort`), serves only `/health/live` and `/health/ready`. A worker opens only the management port. Route the platform's traffic to 8080 only.
- Railway: `docs/deployment.md` at the repository root describes the deployment. `.railway/railway.ts` declares the PostgreSQL service and sets the health check and the variables. `PORT` is 8081, because Railway's health check uses `PORT`. The domain of `api-web` targets 8080.
- The image `apps/api/Dockerfile` has no `HEALTHCHECK`: it has no shell and no `curl` (see the deviations table of `ARCHITECTURE.md`). Set the platform's HTTP health check instead, on port 8081. The readiness path is `/health/ready` and the liveness path is `/health/live`.
- Kubernetes: use `/health/ready` for the readiness probe and `/health/live` for the liveness probe, both on port 8081. The Service exposes only port 8080.
- Set `runAsNonRoot: true`. Both images run as a numeric non-root user.
