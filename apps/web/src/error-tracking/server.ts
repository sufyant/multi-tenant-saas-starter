import { init } from "@sentry/nextjs";
import { type ErrorTracking, serverOptions } from "./options";

/** Starts the server SDK. instrumentation.ts calls it once, only with a DSN (F55). */
export function startServerErrorTracking(tracking: ErrorTracking): void {
  init(serverOptions(tracking));
}
