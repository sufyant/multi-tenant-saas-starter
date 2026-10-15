# Running the API locally

These steps take an empty machine to a created tenant and an accepted invitation. They run one build output as a `web` process and a `worker` process, as section 1 of `ARCHITECTURE.md` describes. Every command runs from the repository root unless it says otherwise.

## 1. What you need

- The .NET SDK named in `apps/api/global.json` (10.0.102 or a later 10.0 feature band).
- PostgreSQL 18 and `psql`.
- An identity provider instance for development. It must serve OpenID Connect discovery (`/.well-known/openid-configuration` and its key set) over HTTPS with a certificate the machine trusts, and its Backend API. You need:
  - its issuer URL (`Authentication:Clerk:Issuer`), its Backend API URL and its secret key (`ControlPlane:Clerk:*`);
  - a way to get a session token for a user. The token carries `sub`, `sid`, `iss`, `exp` and `nbf`; a token without `sid`, such as a JWT template's token, is refused. A token for the system door also carries `fva` with a verified second factor (for example `[0,0]`).
  - open sign-up with email verification at sign-up: the Owner signs up like any other user. The application calls the Backend API only to read a user's verified email addresses when they accept an invitation.
  - a user whose verified email address is the one you put in `ControlPlane:FirstSystemAdminEmail`. That user becomes the first system admin on their first request to `/v1/admin/...` (section 6).

## 2. Database

Start PostgreSQL 18 with a database for the API, for example in a container:

```sh
docker run -d --name api-db -e POSTGRES_PASSWORD=<superuser password> -e POSTGRES_DB=app -p 5432:5432 postgres:18
```

Create the two roles (R2, R7). This is the only step that uses the superuser. Connect to the API's database, not to `postgres`:

```sh
psql "postgresql://postgres:<superuser password>@localhost:5432/app" \
  -v owner_password=<owner password> -v application_password=<application password> \
  -f apps/api/db/bootstrap.sql
```

## 3. Build once

```sh
dotnet publish apps/api/src/Api -c Release -o ./out/api
```

Every process below runs this output. Only the configuration changes.

## 4. Migrate

The migration step runs as the owner role and needs only its connection (R3). `ConnectionStrings:Migrations` is given only to the `migrate` command, never to a running process. It creates every module's schema, the host's `api` schema and the `wolverine` schema, then exits. It logs one line for each module, with the number of migrations it applied, and one line when it has finished. Running it again is safe.

```sh
cd out/api
ConnectionStrings__Migrations="Host=localhost;Port=5432;Database=app;Username=api_owner;Password=<owner password>" \
  dotnet Api.dll migrate
# ... Migrated the api schema: 1 migrations applied
# ... Migrated the control_plane schema: 3 migrations applied
# ... Migrated the audit schema: 3 migrations applied
# ... Migration finished: the schemas api, control_plane, audit and the message storage in wolverine are up to date
```

The application does not start on a database that was not migrated. It stops at once and says so:

```text
The application does not start: the database is not migrated, it has no schema api, audit, control_plane, wolverine: run `dotnet Api.dll migrate` first.
```

The Notifications module stores no data and has no schema of its own. A database migrated by an earlier version still has an empty `notifications` schema, which nothing uses. The owner role can drop it by hand:

```sh
psql "postgresql://api_owner:<owner password>@localhost:5432/app" -c "DROP SCHEMA notifications CASCADE"
```

ControlPlane's schema was `catalog` before it became `control_plane`. A database migrated by an earlier version still has the `catalog` schema. The migration creates `control_plane` empty and copies no rows, so the tenants, users and system admins of `catalog` are not seen any more. The first system admin comes from configuration again (section 6). Nothing uses `catalog`; the owner role can drop it by hand:

```sh
psql "postgresql://api_owner:<owner password>@localhost:5432/app" -c "DROP SCHEMA catalog CASCADE"
```

## 5. Configure

Configuration comes from environment variables (section 7). `apps/api/docs/configuration.md` lists every key: whether it is required, its default and what Development relaxes. Each key of `apps/api/appsettings.Example.json` becomes a variable with `__` in place of `:`, and an array item gets its index (`Host__Cors__AllowedOrigins__0`). For a local run:

```sh
ASPNETCORE_ENVIRONMENT=Development
Host__Role=web                       # web or worker; one process per role
Host__ShutdownTimeout=00:00:30
Host__Cors__AllowedOrigins__0=https://app.localhost
ConnectionStrings__Database=Host=localhost;Port=5432;Database=app;Username=api_application;Password=<application password>
Authentication__Clerk__Issuer=<issuer URL>
Authentication__Clerk__AuthorizedParties__0=https://app.localhost
ForwardedHeaders__KnownProxies__0=10.0.0.1
ForwardedHeaders__KnownNetworks__0=10.0.0.0/8
RateLimiting__PermitLimit=1000
RateLimiting__Window=00:01:00
RateLimiting__InvitationAccept__PermitLimit=10
RateLimiting__InvitationAccept__Window=00:01:00
ControlPlane__FirstSystemAdminEmail=<first system admin's email>
ControlPlane__ActivationTimeout=00:10:00
ControlPlane__InvitationEmailTimeout=02:00:00   # longer than the Notifications retry delays together
ControlPlane__CancellationTimeout=00:10:00
ControlPlane__Clerk__SecretKey=<Backend API secret key>
ControlPlane__Clerk__BackendApiUrl=<Backend API URL, ending in />
ControlPlane__Clerk__Timeout=00:00:10
ControlPlane__Clerk__WebhookSigningSecret=<signing secret of the webhook endpoint, whsec_...>   # web only; section 11
ControlPlane__Invitations__AcceptUrl=https://app.localhost/invitations/accept
ControlPlane__Invitations__Lifetime=7.00:00:00
Notifications__InvitationEmailRetryDelays__0=00:00:10
Notifications__InvitationEmailRetryDelays__1=00:01:00
Notifications__InvitationEmailRetryDelays__2=00:05:00
Notifications__InvitationEmailRetryDelays__3=00:15:00
Notifications__Resend__From=App <no-reply@app.localhost>
Notifications__Resend__Timeout=00:00:10
ASPNETCORE_URLS=http://127.0.0.1:5080   # the public port; each web process on the machine needs its own
Host__ManagementPort=5180               # the health endpoints; each process on the machine needs its own
```

Notes:

- Leave `Notifications__Resend__ApiKey` unset. In Development without it, the worker writes each email to its log instead of sending it. If it is set together with `From`, email is really sent, in Development too.
- Leave `ConnectionStrings__Messaging` unset; Wolverine then uses `ConnectionStrings__Database`.
- Do not give the running processes `ConnectionStrings__Migrations` (R3).
- Leave `OTEL_EXPORTER_OTLP_ENDPOINT` unset unless a collector listens there; without it no telemetry is sent.
- Development relaxes three things: `Authentication:Clerk:AuthorizedParties` may be empty, `Notifications:Resend:*` may be empty (email goes to the log), and the OpenAPI document and Scalar are served on the web process.

## 6. Start the web and the worker

From `out/api`, with the variables above:

```sh
dotnet Api.dll                                         # web: API on 5080, health on 5180
Host__Role=worker Host__ManagementPort=5181 dotnet Api.dll # worker: health on 5181
```

Each process checks its configuration and its database role on start, and stops if a setting is missing or the role may bypass row level security.

Each process needs its own management port. A web process also needs its own public port. A worker opens no public port.

Check both. The health endpoints answer only on the management port (section 8 of `ARCHITECTURE.md`):

```sh
curl http://127.0.0.1:5180/health/live    # Healthy
curl http://127.0.0.1:5180/health/ready   # Healthy
curl http://127.0.0.1:5181/health/ready   # Healthy
curl -i http://127.0.0.1:5080/health/ready   # 404: the public port does not answer them
```

The worker serves only the health endpoints. A web process without a worker warns that no node can run durability agents. Tenant creation then waits in the queue until a worker starts.

For a single process, `Host__Role=all` runs both roles in one process.

## 7. Create a tenant

As the first system admin, with a token that has a verified second factor:

```sh
curl -X POST http://127.0.0.1:5080/v1/admin/tenants \
  -H "Authorization: Bearer <admin token>" \
  -H "Idempotency-Key: <a key of your choice>" \
  -H "Content-Type: application/json" \
  -d '{"name":"Acme","slug":"acme","ownerEmail":"owner@acme.test"}'
```

The answer is `200` with the tenant in `Provisioning`. The first request takes a few seconds, while Wolverine generates its handler code. Within seconds the worker takes the tenant creation to its end, and the worker's log shows the invitation email:

```text
Email to owner@acme.test is not sent in Development: You are invited to Acme
...
https://app.localhost/invitations/accept?code=<tenantId>.<secret>
```

The link is always our accept link. Tenant creation creates nothing at the identity provider: an owner without an account signs up there like any other user, with the invited email address verified, and then accepts.

## 8. Accept the invitation

As the owner, signed in with the invited email address verified:

```sh
curl -X POST http://127.0.0.1:5080/v1/me/invitations/accept \
  -H "Authorization: Bearer <owner token>" -H "Content-Type: application/json" \
  -d '{"code":"<tenantId>.<secret>"}'
curl http://127.0.0.1:5080/v1/me/tenants -H "Authorization: Bearer <owner token>"
curl http://127.0.0.1:5080/v1/tenants/<tenantId>/members -H "Authorization: Bearer <owner token>"
```

## 9. Stop

Send SIGTERM (Ctrl+C) to each process. It stops taking messages and finishes the ones it has within `Host:ShutdownTimeout`.

## 10. Run in containers

The image `apps/api/Dockerfile` runs every process, as section 1 of `ARCHITECTURE.md` describes. The database and the roles come from section 2.

Put the settings of section 5 in a file, `api.env`, one per line. Leave out the comments: Docker keeps a comment as part of the value. Leave out `ASPNETCORE_URLS` and `Host__ManagementPort`: the image listens on port 8080 for the API and on port 8081 for the health endpoints. Write `host.docker.internal` in place of `localhost` in the connection strings, so a container reaches the database on your machine.

```sh
docker build -t api apps/api
docker run --rm --add-host=host.docker.internal:host-gateway \
  -e ConnectionStrings__Migrations="Host=host.docker.internal;Port=5432;Database=app;Username=api_owner;Password=<owner password>" \
  api migrate
docker run --rm --add-host=host.docker.internal:host-gateway --env-file api.env -e Host__Role=web -p 5080:8080 -p 5180:8081 api
docker run --rm --add-host=host.docker.internal:host-gateway --env-file api.env -e Host__Role=worker -p 5181:8081 api
```

`-e Host__Role` overrides the role in `api.env`. The checks of section 6 work on the same ports.

## 11. Receive the identity provider's webhook

The web process keeps the catalog's copy of each user's email address and name fresh from Clerk's webhook, `POST /v1/webhooks/clerk` (A7). Clerk cannot reach `127.0.0.1`, so the Clerk CLI opens a relay and forwards each delivery to the web process. The headers stay the same, so the signature stays valid.

```sh
clerk webhooks listen --token "$(clerk webhooks token)" --forward-to http://127.0.0.1:5080/v1/webhooks/clerk
```

1. The command prints a relay URL. In the Clerk dashboard of the development instance, add a webhook endpoint with that URL. Subscribe it to `user.updated` and `user.deleted`.
2. Copy the endpoint's signing secret into `ControlPlane__Clerk__WebhookSigningSecret` and restart the web process.
3. Change the name of a user who has accepted an invitation, or use the endpoint's "Testing" tab in the dashboard. The web process answers `204`, and the user's row in `control_plane.users` shows the new name.

A delivery without a valid signature, or older than five minutes, answers `400` and changes nothing.


## 12. Change a request or response type

The C# types are the source of every client's types (N11). A change reaches the frontend in three steps. Run them from the repository root.

1. Change the type at the end of its endpoint file (N7).
2. Build the API. The build writes `apps/api/openapi/v1.json`.

   ```sh
   dotnet build apps/api/Api.slnx
   ```

3. Generate the client types. The command reads the committed `openapi/v1.json`, so it needs no .NET.

   ```sh
   pnpm api:generate
   ```

Commit `apps/api/openapi/v1.json` and `packages/api-client/src/schema.ts` with the change. `pnpm check-types` then shows each frontend call that the change breaks. CI fails when either file is not current (`packages/api-client/README.md`).
