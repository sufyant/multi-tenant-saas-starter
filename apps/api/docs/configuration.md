# Configuration

Every setting the API reads. Configuration comes from environment variables (section 7 of `ARCHITECTURE.md`): a key becomes a variable with `__` in place of `:`, and a list item gets its index (`Host__Authentication__AuthorizedParties__0`). `appsettings.json` holds only the defaults below that are marked as coming from it; `appsettings.Example.json` lists the keys with placeholders.

## 1. Settings

Each process checks its settings when it starts. If a required setting is missing or wrong, it stops and names the key. `ConfigurationDocumentTests` fails when a key the code binds has no row here, when `appsettings.Example.json` and this table list different keys, or when a default here differs from the code or from `appsettings.json`.

"Required" means the process does not start without it. **Development** marks what `ASPNETCORE_ENVIRONMENT=Development` relaxes.

| Key | Required | Default | What it does |
| --- | --- | --- | --- |
| `Host:Role` | Required | none | The process type: `web` serves the API, `worker` handles messages, `all` does both in one process (local development and tests) |
| `Host:Idempotency:KeyRetention` | Optional | `1.00:00:00` | How long the host keeps request keys and their results. Must be at least 24 hours. The worker sends a cleanup message each hour. Cleanup keeps an active claim |
| `Host:ShutdownTimeout` | Optional | `00:00:30` | How long a stopping process may take to finish the work it has |
| `Host:ManagementPort` | Optional | `8081` | The port of the health endpoints `/health/live` and `/health/ready`. The process listens on it on all interfaces and answers nothing else there. It must differ from the public port. Each process on one machine needs its own management port |
| `Host:Messaging:UnknownMessages:Delay` | Optional | `00:00:30` | How long a worker waits before a message it has no handler for is offered again. During a rolling deploy, an old worker puts back a message only the new version knows. Must be positive (W12) |
| `Host:Messaging:UnknownMessages:Tries` | Optional | `10` | How many times a worker puts back a message it has no handler for. After that, the message goes to the dead letter queue. Delay times tries must be longer than a deploy. Must be positive (W12) |
| `Host:Messaging:DeadLetters:Retention` | Optional | `14.00:00:00` | How long a message stays in the dead letter queue. Then a worker deletes it. An invitation email's message holds the address and the link with its secret. Must be positive (W13; `docs/deployment.md`, section 14) |
| `Host:RateLimiting:PermitLimit` | Optional | `1000` (`appsettings.json`) | Requests each user may make in a window, in each process |
| `Host:RateLimiting:Window` | Optional | `00:01:00` (`appsettings.json`) | The rate limit's window |
| `Host:RateLimiting:InvitationAccept:PermitLimit` | Optional | `10` | Invitation accepts each user may make in a window, on top of the general limit |
| `Host:RateLimiting:InvitationAccept:Window` | Optional | `00:01:00` | The window of the invitation accept limit |
| `ConnectionStrings:Database` | Required | none | The application role's connection. Requests and handlers use it, and Wolverine too unless `ConnectionStrings:Messaging` is set |
| `ConnectionStrings:Messaging` | Optional | `ConnectionStrings:Database` | The application role's direct or session-mode connection for Wolverine's message store. Needed when `ConnectionStrings:Database` goes through a pooler in transaction mode |
| `ConnectionStrings:Cache` | Optional | none | The Valkey (or Redis) connection of the shared cache, in the StackExchange.Redis form: `host:port,password=...`. Without it there is no cache, and every access check reads the database. Every process that serves requests or handles messages needs the same value: a process without it removes no entry of the rows it changes (`ARCHITECTURE.md`, section 3, "Cache") |
| `ConnectionStrings:Migrations` | Required by `migrate` only | none | The owner role's direct connection. Given only to the `migrate` command, never to a running process (R3) |
| `Host:Authentication:Issuer` | Required | none | The identity provider's issuer URL: the tokens' issuer and the source of their signing keys |
| `Host:Authentication:AuthorizedParties` | Required outside Development. **Development:** may be empty | none | The origins of the clients allowed to use the API (the tokens' `azp`): item 0 is the web app's origin, item 1 the platform app's, which runs on an origin of its own. With none, a token issued to any origin is accepted |
| `Host:ForwardedHeaders:KnownProxies` | Optional | none | IP addresses of trusted reverse proxies. With no proxy and no network, forwarded headers are not read |
| `Host:ForwardedHeaders:KnownNetworks` | Optional | none | Trusted proxy networks in CIDR notation |
| `Host:ErrorTracking:Dsn` | Optional | none | The error tracker's DSN, an absolute URL in the Sentry protocol. With it, each process sends each unhandled exception and each log at `Error` or above to the error tracker, after the privacy filter (section 7 of `ARCHITECTURE.md`, "Error tracking"). Without it, or empty, nothing is sent |
| `Host:ErrorTracking:Release` | Optional | none | The release that each error event names: the commit the image was built from. The container image sets it from its build argument `SOURCE_REVISION`, so an environment does not set it |
| `Host:Heartbeat:Url` | Optional | none | The worker's heartbeat URL, an absolute `http` or `https` URL. A secret: its token is in the path. With it, a worker runs its readiness checks every `Host:Heartbeat:Interval` and calls the URL when they pass (section 7 of `ARCHITECTURE.md`, "Heartbeat"). A web host never calls it. Without it, or empty, nothing runs |
| `Host:Heartbeat:Interval` | Optional | `00:01:00` | The time between two heartbeat calls. Positive. The heartbeat's period holds at least two intervals, so one lost call never alerts |
| `ControlPlane:SystemAdmins:RootEmails` | Required: at least one item | none | The email addresses of the root system admins (`flows/system-admins.md`). Each item is an email address. A person whose verified address is in the list becomes a system admin at their first request to `/v1/platform/...` with a second factor. No system admin can revoke a root system admin. An address taken out of the list lifts that protection at the next start; the person stays a system admin until another system admin revokes them |
| `ControlPlane:SystemAdmins:ReverificationWindow` | Optional | `00:10:00` | How long ago a system admin's second factor may have been verified for `GrantSystemAdmin` and `RevokeSystemAdmin` (A6a). From 1 to 10 minutes, Clerk's range for reverification; the default is Clerk's strict level. An older second factor answers 403 `system_admin.reverification_required`, and the platform asks the person to verify again |
| `ControlPlane:Clerk:SecretKey` | Required | none | The identity provider's Backend API secret key |
| `ControlPlane:Clerk:BackendApiUrl` | Optional | `https://api.clerk.com/v1/` | The identity provider's Backend API URL, ending in `/` |
| `ControlPlane:Clerk:Timeout` | Optional | `00:00:10` | How long a Backend API call may take |
| `ControlPlane:IdentityProvider:RetryDelays` | Optional | `00:00:10`, `00:01:00`, `00:05:00`, `00:15:00` | The pauses before each new try of a call to the identity provider that failed: letting an invited address sign up, withdrawing it, deleting a user. Positive, and each longer than the one before. Configured pauses replace the defaults. After the last try the message goes to the dead letter queue |
| `ControlPlane:Invitations:AcceptUrl` | Required | none | The frontend page that accepts an invitation. The invitation code is added as the `code` parameter |
| `ControlPlane:Invitations:Lifetime` | Optional | `7.00:00:00` | How long an invitation can be accepted. At least one day. The invitation email writes the moment it ends, never the lifetime |
| `ControlPlane:Invitations:DailyLimitPerTenant` | Optional | `50` | How many invitations a tenant may create in a rolling window of `InviteMemberHandler.LimitWindow`, cancelled and replaced ones included. Positive |
| `ControlPlane:Tenants:ReopenPeriod` | Optional | `30.00:00:00` | How long after its closure a system admin can reopen a tenant (K4). When it ends, the tenant purge starts. Positive |
| `ControlPlane:Tenants:PurgeWarningLead` | Optional | `7.00:00:00` | How long before the purge the closed tenant's Owners get the warning email: the closure schedules it at the closure plus the reopen period less this lead (`flows/tenant-purge.md`). `GetTenant` answers that moment as `purgeWarningAt`. Positive and shorter than `ControlPlane:Tenants:ReopenPeriod` |
| `ControlPlane:Tenants:PurgeTimeout` | Optional | `01:00:00` | How long the tenant purge waits for its steps before its alarm names the steps it still waits for; it keeps waiting after the alarm (S10; `flows/tenant-purge.md`). Positive |
| `Notifications:InvitationEmails:RetryDelays` | Optional | `00:00:10`, `00:01:00`, `00:05:00`, `00:15:00` | The pauses before each new try of an invitation email, to a tenant's first owner or to a person a member invited, and of the warning email before a tenant's purge. Each is positive and longer than the one before |
| `Notifications:Resend:ApiKey` | Required outside Development. **Development:** may be empty | none | Resend's API key. In Development without it, or without `From`, email is written to the log. With both set, email is really sent, in Development too |
| `Notifications:Resend:From` | Required outside Development. **Development:** may be empty | none | The sender, such as `App <no-reply@example.com>`, on a domain verified at Resend |
| `Notifications:Resend:ApiUrl` | Optional | `https://api.resend.com/` | Resend's API URL |
| `Notifications:Resend:Timeout` | Optional | `00:00:10` | How long a send may take |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optional | none | The OTLP collector's URL for logs, metrics and traces. Without it no telemetry is sent |
| `Serilog:MinimumLevel:Default` | Optional | `Information` (`appsettings.json`) | The lowest level written to the log |
| `Serilog:MinimumLevel:Override:<category>` | Optional | `Warning` for `Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, `Wolverine` and `JasperFx` (`appsettings.json`) | The lowest level for one log category. Each handled message is logged at `Information` under the message type's name |
| `AllowedHosts` | Optional | `*` (`appsettings.json`) | The host names the API answers to |
| `ASPNETCORE_ENVIRONMENT` | Optional | `Production` | The environment. `Development` relaxes the settings marked above |
| `ASPNETCORE_URLS` | Optional | `http://localhost:5000`. In the container image: port 8080 on all interfaces (the base image sets `ASPNETCORE_HTTP_PORTS=8080`) | The public address and port of the web process: the API, and in Development the OpenAPI document and Scalar. A worker opens no public port. Each process on one machine needs its own port |

The API turns GSSAPI encryption off on every PostgreSQL connection. A connection string that names `GSS Encryption Mode` keeps its value. TLS protects the connection. The image has no Kerberos library, and with Npgsql's default each process prints a false error.

## 2. What Development relaxes

- `Host:Authentication:AuthorizedParties` may be empty.
- `Notifications:Resend:ApiKey` and `Notifications:Resend:From` may be empty; email then goes to the log.
- The web process serves the OpenAPI document (`/openapi/v1.json`) and Scalar (`/scalar`). Outside Development both answer 404.

Development does not relax the token's signature, issuer and lifetime, the second factor for the system door, or the check of the database role (R10).
