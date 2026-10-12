// Railway settings of the API, as code (Railway docs, "Infrastructure as Code").
//
// This file manages the two API services of the environment that the Railway CLI is linked to:
// their names, region, replicas, health check and variables.
//
// It does not manage:
// - The image. CI sets it on each deploy, by digest (docs/deployment.md). So no service has a
//   `source` here.
// - The values. Each product sets its own values as shared variables of the environment. This
//   file references each one by name and never holds a value (The Twelve-Factor App, factor III).
//
// Apply deletes a service variable that is not in this file. Add every variable of the two
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
import { defineRailway, project, service } from "railway/iac";

// Every key that apps/api/docs/configuration.md marks as required outside Development, except
// Host:Role (set per service below) and ConnectionStrings:Migrations (CI only, R3). Two optional
// keys are added: ConnectionStrings:Messaging, because the Database connection goes through
// Neon's pooler, and Host:Cors:AllowedOrigins:0, for the web app's origin. Each shared variable
// must exist: a reference to a missing one gives an empty value (docs/deployment.md, section 7).
// The `__` form is the environment variable form of a key.
const sharedVariables = [
  "ConnectionStrings__Database",
  "ConnectionStrings__Messaging",
  "Authentication__Clerk__Issuer",
  "Authentication__Clerk__AuthorizedParties__0",
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

  // One build output, two process roles (W5). The worker serves the health endpoints too
  // (apps/api/docs/local-run.md, section 6).
  const api = (name: string, role: "web" | "worker") =>
    service(name, {
      replicas: { "europe-west4-drams3a": 1 },
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
    resources: [api("api-web", "web"), api("api-worker", "worker")],
  });
});
