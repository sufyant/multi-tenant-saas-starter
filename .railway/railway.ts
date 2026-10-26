// Railway settings of the API and the web apps, as code (Railway docs, "Infrastructure as Code").
//
// This file manages the services of the environment that the Railway CLI is linked to:
// - the PostgreSQL service `postgres`, from Railway's template, with its region;
// - the cache service `valkey`, from the official Valkey image, with its region, start command and memory limit;
// - the two API services: their names, region, replicas, health check and variables;
// - the two web apps, `web` and `platform`: the same, from .railway/web.json and .railway/platform.json.
//
// It does not manage:
// - The image of each service. CI sets it on each deploy, by digest (docs/deployment.md). So
//   no service has a `source` here.
// - The domains. Each environment has its own custom domains, which stay out of the repository. They are
//   Railway service settings (docs/deployment.md, section 4).
// - The database's public access, backups and point-in-time recovery. The `postgres` helper cannot
//   hold them. docs/deployment.md, section 4, lists them as manual steps.
// - The values. Each product sets its own values as shared variables of the environment. This
//   file references each one by name and never holds a value (The Twelve-Factor App, factor III).
//
// Apply deletes a service variable that is not in this file. Add every variable of every service
// here, not in the Railway dashboard.
//
// The same file serves the environments staging and production. Only the environment names differ, from
// `ctx.environment`: the API's ASPNETCORE_ENVIRONMENT and the web apps' ERROR_TRACKING_ENVIRONMENT.
//
// Apply it by hand to each environment, from the repository root, after `railway login` and
// `railway link` to that environment:
//   pnpm railway:plan     shows the changes and changes nothing
//   pnpm railway:apply    applies the changes
// Read the plan first. After the first deploy, do not apply: follow docs/deployment.md, section 7.
import { defineRailway, image, postgres, project, service } from "railway/iac";
// The values that the deploy scripts read too: the service names, the management port and the
// health path. The API's own defaults are the source of the port and the path;
// InfrastructureValueTests in apps/api checks that this file names the same.
import infrastructure from "./api.json" with { type: "json" };
// The same values of each web app: its service name, port and health path. route.test.ts in each app checks the path, and
// env.test.ts in each app checks what this file gives the service.
import platform from "./platform.json" with { type: "json" };
// The cache's service name, image and port. The API's test fixtures run the same image, and InfrastructureValueTests checks that
// the image is pinned by digest.
import valkey from "./valkey.json" with { type: "json" };
import web from "./web.json" with { type: "json" };

// Every key that apps/api/docs/configuration.md marks as required outside Development, except
// Host:Role (set per service below) and ConnectionStrings:Migrations (CI only, R3).
// Host:Authentication:AuthorizedParties has two items:
// 0 is the web app's origin and 1 the platform app's, because the platform runs on an origin of its own and the API checks the
// token's party. ConnectionStrings:Database
// connects directly to the environment's PostgreSQL service, with no pooler, so Wolverine uses it
// too and ConnectionStrings:Messaging is not set. Each shared variable must exist: a reference to
// a missing one gives an empty value (docs/deployment.md, section 7). The `__` form is the
// environment variable form of a key.
//
// No API service references a variable of the `postgres` service. DATABASE_URL, PGUSER and
// PGPASSWORD hold the superuser; the API connects as api_application only (R2, R7).
//
// ConnectionStrings:Cache is optional, and the API services get it below, from the `valkey` service. Without it the API reads every
// access check from the database.
//
// Host:ErrorTracking:Dsn is optional: the API sends its errors to the error tracker only with it. A
// missing shared variable gives an empty value, and an empty DSN turns error tracking off.
//
// ConfigurationDocumentTests in apps/api checks that this file sets every required key, and only
// keys that configuration.md documents.
const sharedVariables = [
  "ConnectionStrings__Database",
  "Host__Authentication__Issuer",
  "Host__Authentication__AuthorizedParties__0",
  "Host__Authentication__AuthorizedParties__1",
  "ControlPlane__SystemAdmins__RootEmails__0",
  "ControlPlane__Clerk__SecretKey",
  "ControlPlane__Invitations__AcceptUrl",
  "Notifications__Resend__ApiKey",
  "Notifications__Resend__From",
  "Host__ErrorTracking__Dsn",
];

// ASP.NET Core's name of each environment, capitalised as its standard names are, so that a file such as
// appsettings.Staging.json loads on Linux, where file names are case sensitive. The error tracker names
// the environment of each event from it (apps/api/docs/ARCHITECTURE.md, section 7, "Error tracking").
const aspNetEnvironments: Record<string, string> = {
  staging: "Staging",
  production: "Production",
};

export default defineRailway((ctx) => {
  if (!ctx.projectName) {
    throw new Error("No Railway project is linked. Run `railway link` first.");
  }

  const aspNetEnvironment = aspNetEnvironments[ctx.environment ?? ""];
  if (!aspNetEnvironment) {
    throw new Error(
      `The Railway environment "${ctx.environment}" has no ASP.NET Core environment name.`,
    );
  }

  // The API's region. The database runs there too, next to the API.
  const region = "europe-west4-drams3a";

  // Railway's PostgreSQL template: the image ghcr.io/railwayapp-templates/postgres-ssl:18, which
  // pins the major version that CI tests against, and a volume that Railway attaches. Each
  // environment gets its own server, so two environments never share a role or data
  // (docs/deployment.md, section 3).
  const database = postgres("postgres", { region });

  // The shared cache of the API's access checks (apps/api/docs/ARCHITECTURE.md, C9). Valkey is BSD licensed and
  // speaks the Redis protocol. It is a cache only, so it has no volume and saves nothing: a lost cache refills from the
  // database. It has no public domain and no TCP proxy, so only the private network reaches it.
  // - VALKEY_PASSWORD: a shared variable that the maintainer creates once with a random value (docs/deployment.md, section 17).
  //   The start command reads it, and the API's connection string references it, so the password has one source. A missing
  //   shared variable gives an empty value, and an empty password would turn authentication off, so Valkey does not start then.
  // - The container's memory limit is 512 MB, and Valkey keeps its data under 384 MB, with room for its own buffers.
  //   Railway's default sets no limit. When the data reaches the limit, Valkey evicts the key with the shortest time to live
  //   (volatile-ttl, Valkey docs, "Key eviction"). Every key has one, and an entry that a removal outdated always has less time left
  //   than the removal's marker, so Valkey evicts the old entry before the marker (apps/api/docs/ARCHITECTURE.md, C9).
  // - --save "" and --appendonly no turn persistence off, so Valkey never forks to write a snapshot.
  const cache = service(valkey.service, {
    source: image(valkey.image),
    replicas: { [region]: 1 },
    start: `/bin/sh -c '[ -n "$VALKEY_PASSWORD" ] || { echo "VALKEY_PASSWORD is empty" >&2; exit 1; }; exec valkey-server --port ${valkey.port} --requirepass "$VALKEY_PASSWORD" --maxmemory 384mb --maxmemory-policy volatile-ttl --save "" --appendonly no'`,
    deploy: {
      limitOverride: { containers: { memoryBytes: 512 * 1024 * 1024 } },
    },
    env: {
      VALKEY_PASSWORD: ctx.shared.VALKEY_PASSWORD,
    },
  });

  // One build output, two process roles (W5). The worker serves the health endpoints too
  // (apps/api/docs/local-run.md, section 6). `own` holds the variables of one service alone.
  const api = (
    name: string,
    role: "web" | "worker",
    own: Record<string, string> = {},
  ) =>
    service(name, {
      replicas: { [region]: 1 },
      healthcheck: infrastructure.healthPath,
      env: {
        Host__Role: role,
        ASPNETCORE_ENVIRONMENT: aspNetEnvironment,
        // Railway's health check uses PORT; the public domain targets 8080. The API ignores PORT and
        // answers the health endpoints only on its management port (Railway docs, "Healthchecks",
        // "Configure the healthcheck port").
        PORT: String(infrastructure.managementPort),
        // The web host reads its access checks through the cache, and both hosts remove the entries of the rows they change,
        // so both reach Valkey over the private network. Plain TCP, because WireGuard encrypts private traffic (Railway docs,
        // "How Private Networking Works").
        ConnectionStrings__Cache: `\${{${valkey.service}.RAILWAY_PRIVATE_DOMAIN}}:${valkey.port},password=\${{${valkey.service}.VALKEY_PASSWORD}}`,
        ...Object.fromEntries(
          sharedVariables.map((name) => [name, ctx.shared[name]]),
        ),
        ...own,
      },
    });

  // A web app: the Next.js server of apps/web or apps/platform. It reads every variable of its .env.example at start (F7).
  // - API_URL: the BFF reaches api-web over the environment's private network, never its public domain. A Railway
  //   reference to api-web's private domain, on the API's public port, resolved by Railway; plain http, because WireGuard
  //   encrypts private traffic (Railway docs, "How Private Networking Works").
  // - CLERK_SECRET_KEY: the same shared variable that the API reads as ControlPlane__Clerk__SecretKey, so the key has one source.
  // - CLERK_PUBLISHABLE_KEY: a shared variable of its own, read at run time, so one image serves every environment.
  // - PORT: the port of the server and of Railway's health check.
  // - ERROR_TRACKING_DSN: the DSN of the app's own Errors application in Better Stack, a shared variable of its own:
  //   WEB_ERROR_TRACKING_DSN or PLATFORM_ERROR_TRACKING_DSN. Without it the app sends no error (F55).
  // - ERROR_TRACKING_ENVIRONMENT: the environment tag of each error event, the API's ASP.NET Core name in lower case, so the
  //   three services tag their events alike.
  const app = (settings: typeof web) =>
    service(settings.service, {
      replicas: { [region]: 1 },
      healthcheck: settings.healthPath,
      env: {
        PORT: String(settings.port),
        API_URL: `http://\${{${infrastructure.services.web}.RAILWAY_PRIVATE_DOMAIN}}:${infrastructure.publicPort}`,
        CLERK_PUBLISHABLE_KEY: ctx.shared.CLERK_PUBLISHABLE_KEY,
        CLERK_SECRET_KEY: ctx.shared.ControlPlane__Clerk__SecretKey,
        ERROR_TRACKING_DSN:
          ctx.shared[`${settings.service.toUpperCase()}_ERROR_TRACKING_DSN`],
        ERROR_TRACKING_ENVIRONMENT: aspNetEnvironment.toLowerCase(),
      },
    });

  return project(ctx.projectName, {
    resources: [
      database,
      cache,
      api(infrastructure.services.web, "web"),
      // Host:Heartbeat:Url is optional and for the worker alone: the web host's uptime monitor calls a
      // public route, and the worker answers none, so its heartbeat is its alert when it stops. The URL holds
      // the heartbeat's token, so it is a secret. A missing shared variable gives an empty value, and an
      // empty URL turns the heartbeat off (apps/api/docs/ARCHITECTURE.md, section 7, "Heartbeat").
      api(infrastructure.services.worker, "worker", {
        Host__Heartbeat__Url: ctx.shared.Host__Heartbeat__Url,
      }),
      app(web),
      app(platform),
    ],
  });
});
