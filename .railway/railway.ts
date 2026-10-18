// Railway settings of the API, as code (Railway docs, "Infrastructure as Code").
//
// This file manages the services of the environment that the Railway CLI is linked to:
// - the PostgreSQL service `postgres`, from Railway's template, with its region;
// - the two API services: their names, region, replicas, health check and variables.
//
// It does not manage:
// - The image of the API services. CI sets it on each deploy, by digest (docs/deployment.md). So
//   no API service has a `source` here.
// - The database's public access, backups and point-in-time recovery. The `postgres` helper cannot
//   hold them. docs/deployment.md, section 4, lists them as manual steps.
// - The values. Each product sets its own values as shared variables of the environment. This
//   file references each one by name and never holds a value (The Twelve-Factor App, factor III).
//
// Apply deletes a service variable that is not in this file. Add every variable of the two API
// services here, not in the Railway dashboard.
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

// Every key that apps/api/docs/configuration.md marks as required outside Development, except
// Host:Role (set per service below) and ConnectionStrings:Migrations (CI only, R3). One optional
// key is added: Host:Cors:AllowedOrigins:0, for the web app's origin. Authentication:Clerk:AuthorizedParties has two items:
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
  "Authentication__Clerk__Issuer",
  "Authentication__Clerk__AuthorizedParties__0",
  "Authentication__Clerk__AuthorizedParties__1",
  "ControlPlane__FirstSystemAdminEmail",
  "ControlPlane__Clerk__SecretKey",
  "ControlPlane__Invitations__AcceptUrl",
  "Notifications__Resend__ApiKey",
  "Notifications__Resend__From",
  "Host__Cors__AllowedOrigins__0",
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

  return project(ctx.projectName, {
    resources: [
      database,
      api(infrastructure.services.web, "web"),
      api(infrastructure.services.worker, "worker"),
    ],
  });
});
