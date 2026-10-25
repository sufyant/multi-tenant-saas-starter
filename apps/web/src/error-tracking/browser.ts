import { init } from "@sentry/nextjs";
import { browserOptions, type ErrorTracking } from "./options";

/**
 * The DSN and the environment that the root layout writes on `<html>` at run time (F55). The parser reads that element before any
 * script, so instrumentation-client.ts finds them before React starts.
 */
export function browserTracking(
  dataset: DOMStringMap,
): ErrorTracking | undefined {
  const { errorTrackingDsn: dsn, errorTrackingEnvironment: environment } =
    dataset;
  return dsn && (environment === "staging" || environment === "production")
    ? { dsn, environment }
    : undefined;
}

/** Starts the browser SDK when the page names a DSN. */
export function startBrowserErrorTracking(dataset: DOMStringMap): void {
  const tracking = browserTracking(dataset);
  if (tracking) init(browserOptions(tracking));
}
