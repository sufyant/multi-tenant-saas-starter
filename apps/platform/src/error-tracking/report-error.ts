import { captureException, captureRequestError } from "@sentry/nextjs";

/**
 * Sends an error that someone must act on (F55). Features call this, never the SDK. While error tracking is off (no DSN), the SDK has
 * no client and nothing happens.
 */
export function reportError(error: unknown): void {
  captureException(error);
}

/**
 * Sends the error of an error page when it happened in the browser. A server error reaches the page with only its digest, and
 * `onRequestError` sent it already (F55).
 */
export function reportBrowserError(error: Error & { digest?: string }): void {
  if (!error.digest) reportError(error);
}

/** Sends an error of a server component, a route handler or the proxy, as Next.js gives it to `onRequestError` (F55). */
export const reportRequestError = captureRequestError;
