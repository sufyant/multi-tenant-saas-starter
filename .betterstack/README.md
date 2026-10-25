# Better Stack settings

Better Stack holds the error tracking, the uptime checks and the alerts of the API and the web apps. `config.json` holds the settings that Better Stack's API can manage. `scripts/betterstack/config.sh` plans and applies them (`pnpm betterstack:plan`, `pnpm betterstack:apply`). `docs/deployment.md`, section 16, holds the first setup and the manual steps.

The file holds no value: no URL, no token and no email address. The URLs come from `API_URL`, `WEB_URL` and `PLATFORM_URL`, the GitHub environment variables that `deploy.yml` reads. Email alerts go to the members of the Better Stack team, so the owner comes from the account.

## What the API manages

Checked on 2026-10-25 against Better Stack's API documentation and its Terraform providers (`BetterStackHQ/terraform-provider-better-uptime` and `terraform-provider-logtail`, `docs/resources/`).

| Setting | The API manages it | Where it lives | Source |
| --- | --- | --- | --- |
| Errors applications | Yes: list, create, update, delete. `platform` cannot change after creation | `config.json` | Better Stack docs, Errors API, "Applications" |
| Uptime monitors | Yes: list, create, update, delete, with the URL, the expected status, the check interval, the confirmation period and the alert channels | `config.json` | Better Stack docs, Uptime API, "Monitors" |
| Escalation policies and severities | Yes (`/api/v3/policies`, `/api/v2/urgencies`). Not used: the free plan has no escalations | none | Better Stack docs, Uptime API, "Escalation policies", "Severities" |
| Error alerts: a new error, a spike | No. The Errors Slack integration and the Errors to Uptime integration have no API and no Terraform resource | Manual step (`docs/deployment.md`, section 16) | Better Stack docs, Errors, "Slack", "Reporting incidents" |
| Spike protection (the baseline multiplier) | No. Errors, the application, "Advanced settings" | Better Stack's default, 10x | Better Stack docs, Errors, "Spike protection" |
| The DSN of an application | Read only. The list answer holds the application's token, so the script never prints it | Railway sealed variables | Better Stack docs, Errors, "Sentry SDK" |

## The plan

The account is on the free plan. Better Stack's pricing page lists for it "Slack & e-mail alerts", 10 monitors and heartbeats, and 100,000 exceptions a month. Push notifications of the mobile app, escalations, phone calls and SMS need a paid responder seat. The free plan checks a monitor every 3 minutes at most (Better Stack docs, "Check frequency"). So:

- Each monitor alerts by email, with no escalation policy.
- Error alerts go to Slack, through the Errors Slack integration, set up by hand.
- Push, phone and SMS come with production (step 47), with a responder seat.

## The tokens

Better Stack has no read-only or scoped token. A team-based token is limited to one team and one product; a global token manages every team and every product. The script uses two team-based tokens, the narrowest that work:

- `BETTERSTACK_UPTIME_TOKEN`: an "Incidents API" token of the team, for the monitors.
- `BETTERSTACK_TELEMETRY_TOKEN`: a "Telemetry API" token of the team, for the Errors applications.

Keep both in a password manager. They never go into the repository, a GitHub secret or Railway. Source: Better Stack docs, "Getting an API token" (Uptime and Errors).

## What the file holds

- `errors.applications`: one Errors application per app, for every environment. The `environment` tag of each event separates staging and production (the Sentry SDK's `environment` option).
- `uptime.monitors`: one monitor per app and environment. The script names each monitor `<environment> <name>`. `url_variable` names the variable that holds the app's public URL, and `path` the route. The API's monitor calls the public route of `scripts/deploy/smoke-test.sh` and expects its 401, because the health endpoints answer only on the management port. The web apps' monitors call the health path of `.railway/web.json` and `.railway/platform.json`.
- `uptime.defaults`: the settings that every monitor shares.

## Decisions

- **Check every 180 seconds.** The free plan's shortest interval. A staging outage has no users to hurt, so a shorter interval buys nothing.
- **Alert after 300 seconds of failure** (`confirmation_period`). A deploy restarts a service, and a short network fault fails one check. Neither needs a person. Five minutes of failure is an outage that someone must look at. **Resolve after 180 seconds up** (`recovery_period`), one check. Source: Google, *Site Reliability Engineering*, chapter 6, "Monitoring Distributed Systems": alert on symptoms, and only when a person must act.
- **Delete a monitor that is not in the file**, as Railway's `apply` deletes a variable that is not in `.railway/railway.ts`. Only the monitors of the environment are compared: those whose name starts with `<environment> `. A monitor of another environment, or with another name, is never touched.
- **Leave an Errors application that is not in the file**, and print that it is left. Deleting an application deletes its errors.
