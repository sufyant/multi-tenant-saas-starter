// Railway settings of the API and the web apps, as code (Railway docs, "Infrastructure as Code").
//
// This file manages the services of the environment that the Railway CLI is linked to:
// - the PostgreSQL service `postgres`, from Railway's template, with its region;
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
// The same file serves the environments staging and production. No setting differs today; a
// setting that must differ can read `ctx.environment`.
//
// Apply it by hand to each environment, from the repository root, after `railway login` and
// `railway link` to that environment:
//   pnpm railway:plan     shows the changes and changes nothing
//   pnpm railway:apply    applies the changes
// Read the plan first. After the first deploy, do not apply: follow docs/deployment.md, section 7.
import { defineRailway, postgres, project, service } from "railway/iac";
// The values that the deploy scripts read too: the service names, the management port and the
// health path. The API's own defaults are the source of the port and the path;
// InfrastructureValueTests in apps/api checks that this file names the same.
import infrastructure from "./api.json" with { type: "json" };
// The same values of each web app: its service name, port and health path. route.test.ts in each app checks the path, and
// env.test.ts in each app checks what this file gives the service.
import platform from "./platform.json" with { type: "json" };
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
];

export default defineRailway((ctx) => {
  if (!ctx.projectName) {
    throw new Error("No Railway project is linked. Run `railway link` first.");
  }

  // The API's region. The database runs there too, next to the API.
  const region = "europe-west4-drams3a";

  // Railway's PostgreSQL template: the image ghcr.io/railwayapp-templates/postgres-ssl:18, which
  // pins the major version that CI tests against, and a volume that Railway attaches. Each
  // environment gets its own server, so two environments never share a role or data
  // (docs/deployment.md, section 3).
  const database = postgres("postgres", { region });

  // One build output, two process roles (W5). The worker serves the health endpoints too
  // (apps/api/docs/local-run.md, section 6).
  const api = (name: string, role: "web" | "worker") =>
    service(name, {
      replicas: { [region]: 1 },
      healthcheck: infrastructure.healthPath,
      env: {
        Host__Role: role,
        ASPNETCORE_ENVIRONMENT: "Production",
        // Railway's health check uses PORT; the public domain targets 8080. The API ignores PORT and
        // answers the health endpoints only on its management port (Railway docs, "Healthchecks",
        // "Configure the healthcheck port").
        PORT: String(infrastructure.managementPort),
        ...Object.fromEntries(
          sharedVariables.map((name) => [name, ctx.shared[name]]),
        ),
      },
    });

  // A web app: the Next.js server of apps/web or apps/platform. It reads every variable of its .env.example at start (F7).
  // - API_URL: the BFF reaches api-web over the environment's private network, never its public domain. A Railway
  //   reference to api-web's private domain, on the API's public port, resolved by Railway; plain http, because WireGuard
  //   encrypts private traffic (Railway docs, "How Private Networking Works").
  // - CLERK_SECRET_KEY: the same shared variable that the API reads as ControlPlane__Clerk__SecretKey, so the key has one source.
  // - CLERK_PUBLISHABLE_KEY: a shared variable of its own, read at run time, so one image serves every environment.
  // - PORT: the port of the server and of Railway's health check.
  const app = (settings: typeof web) =>
    service(settings.service, {
      replicas: { [region]: 1 },
      healthcheck: settings.healthPath,
      env: {
        PORT: String(settings.port),
        API_URL: `http://\${{${infrastructure.services.web}.RAILWAY_PRIVATE_DOMAIN}}:${infrastructure.publicPort}`,
        CLERK_PUBLISHABLE_KEY: ctx.shared.CLERK_PUBLISHABLE_KEY,
        CLERK_SECRET_KEY: ctx.shared.ControlPlane__Clerk__SecretKey,
      },
    });

  return project(ctx.projectName, {
    resources: [
      database,
      api(infrastructure.services.web, "web"),
      api(infrastructure.services.worker, "worker"),
      app(web),
      app(platform),
    ],
  });
});
