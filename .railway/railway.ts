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
// Read the plan first. Do not apply a plan that changes the `source` of a service
// (docs/deployment.md, section 7).
import { defineRailway, postgres, project, service } from "railway/iac";

// Every key that apps/api/docs/configuration.md marks as required outside Development, except
// Host:Role (set per service below) and ConnectionStrings:Migrations (CI only, R3). One optional
// key is added: Host:Cors:AllowedOrigins:0, for the web app's origin. ConnectionStrings:Database
// connects directly to the environment's PostgreSQL service, with no pooler, so Wolverine uses it
// too and ConnectionStrings:Messaging is not set. Each shared variable must exist: a reference to
// a missing one gives an empty value (docs/deployment.md, section 7). The `__` form is the
// environment variable form of a key.
//
// No API service references a variable of the `postgres` service. DATABASE_URL, PGUSER and
// PGPASSWORD hold the superuser; the API connects as api_application only (R2, R7).
const sharedVariables = [
  "ConnectionStrings__Database",
  "Authentication__Clerk__Issuer",
  "Authentication__Clerk__AuthorizedParties__0",
  "ControlPlane__FirstSystemAdminEmail",
  "ControlPlane__Clerk__SecretKey",
  "ControlPlane__Clerk__WebhookSigningSecret",
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
      healthcheck: "/health/ready",
      env: {
        Host__Role: role,
        ASPNETCORE_ENVIRONMENT: "Production",
        // Railway's health check uses PORT; the public domain targets 8080. The API ignores PORT and
        // answers the health endpoints only on its management port, 8081 (Railway docs,
        // "Healthchecks", "Configure the healthcheck port").
        PORT: "8081",
        ...Object.fromEntries(
          sharedVariables.map((name) => [name, ctx.shared[name]]),
        ),
      },
    });

  return project(ctx.projectName, {
    resources: [database, api("api-web", "web"), api("api-worker", "worker")],
  });
});
