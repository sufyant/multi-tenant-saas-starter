import { captureException, captureRequestError } from "@sentry/nextjs";

/**
 * Sends an error that someone must act on (F55). Features call this, never the SDK. While error tracking is off (no DSN), the SDK has
 * no client and nothing happens.
 */
export function reportError(error: unknown): void {
  captureException(error);
}

/** Sends an error of a server component, a route handler or the proxy, as Next.js gives it to `onRequestError` (F55). */
export const reportRequestError = captureRequestError;
