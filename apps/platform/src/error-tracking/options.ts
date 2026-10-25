import type { BrowserOptions, NodeOptions } from "@sentry/nextjs";
import { isNotSent } from "./not-sent";
import { scrubBreadcrumb, scrubEvent } from "./privacy";

/** Where the events go: the DSN and the environment that the server reads at run time (F55). */
export type ErrorTracking = {
  dsn: string;
  environment: "staging" | "production";
};

// Errors only (F55): the integrations that catch an error, describe it or record a breadcrumb stay on. Each other default
// integration of the SDK is off, such as sessions, tracing, the list of modules and the local variables of a stack frame.
export const browserIntegrations = [
  "EventFilters",
  "FunctionToString",
  "BrowserApiErrors",
  "Breadcrumbs",
  "Console",
  "GlobalHandlers",
  "LinkedErrors",
  "Dedupe",
  "HttpContext",
  "NextjsClientStackFrameNormalization",
];

export const serverIntegrations = [
  "EventFilters",
  "FunctionToString",
  "LinkedErrors",
  "Dedupe",
  "RequestData",
  "NodeSystemError",
  "Console",
  "Http",
  "NodeFetch",
  "OnUncaughtException",
  "OnUnhandledRejection",
  "Context",
  "DistDirRewriteFrames",
];

const only =
  (names: readonly string[]) =>
  <T extends { name: string }>(integrations: T[]): T[] =>
    integrations.filter((integration) => names.includes(integration.name));

// The options of both runtimes: no personal data of any category, no report about the SDK itself, no traces (no
// tracesSampleRate), and one filter for what is not sent and one for privacy.
const common = ({ dsn, environment }: ErrorTracking) => ({
  dsn,
  environment,
  dataCollection: {
    userInfo: false,
    cookies: false,
    httpHeaders: false,
    httpBodies: [],
    urlQueryParams: false,
    graphQL: { document: false, variables: false },
    genAI: { inputs: false, outputs: false },
    mcp: { inputs: false, outputs: false },
    databaseQueryData: false,
    queues: false,
    stackFrameVariables: false,
    frameContextLines: 0,
  },
  sendClientReports: false,
  beforeSend: (
    event: Parameters<NonNullable<BrowserOptions["beforeSend"]>>[0],
    hint: Parameters<NonNullable<BrowserOptions["beforeSend"]>>[1],
  ) => (isNotSent(hint.originalException) ? null : scrubEvent(event)),
  beforeBreadcrumb: scrubBreadcrumb,
});

/** The browser SDK's options. */
export const browserOptions = (tracking: ErrorTracking): BrowserOptions => ({
  ...common(tracking),
  integrations: only(browserIntegrations),
});

/** The server SDK's options: server components, route handlers and the proxy, which runs on Node.js. */
export const serverOptions = (tracking: ErrorTracking): NodeOptions => ({
  ...common(tracking),
  integrations: only(serverIntegrations),
});
