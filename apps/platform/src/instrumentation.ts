import type { Instrumentation } from "next";
import { reportRequestError } from "./error-tracking/report-error";

/**
 * Runs once when the server starts: a missing or wrong environment variable stops the start here (F7). With a DSN, the server's
 * error tracking starts (F55). Every server code runs on Node.js, the proxy too.
 */
export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    const { env } = await import("./env");
    const { ERROR_TRACKING_DSN: dsn, ERROR_TRACKING_ENVIRONMENT: environment } =
      env();
    if (dsn && environment) {
      const { startServerErrorTracking } = await import(
        "./error-tracking/server"
      );
      startServerErrorTracking({ dsn, environment });
    }
  }
}

export const onRequestError: Instrumentation.onRequestError = async (
  err,
  request,
  context,
) => {
  const message = err instanceof Error ? err.message : String(err);
  const digest =
    typeof err === "object" && err !== null && "digest" in err
      ? String(err.digest)
      : undefined;

  console.error("[onRequestError]", {
    message,
    digest,
    path: request.path,
    method: request.method,
    routerKind: context.routerKind,
    routePath: context.routePath,
    routeType: context.routeType,
  });
  reportRequestError(err, request, context);
};
