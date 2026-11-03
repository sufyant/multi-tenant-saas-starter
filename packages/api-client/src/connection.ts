import type { QueryClient } from "@tanstack/react-query";
import { ProblemDetailsError } from "./mutator";

/** The answers of a server or a proxy that is down or overloaded: the connection bar's "isn't answering" (the Connection bar). */
const unreachable = new Set([502, 503, 504]);

/**
 * True when no server answered the call: fetch rejected (a TypeError, the browser could not reach the BFF), or the BFF or a
 * proxy answered 502, 503 or 504. A cancelled call is no failure, and an answer of the API, a 500 too, is the API's.
 */
export function isServerUnreachable(error: unknown): boolean {
  if (error instanceof ProblemDetailsError)
    return unreachable.has(error.status);
  return error instanceof TypeError;
}

export type ServerReach = {
  subscribe: (changed: () => void) => () => void;
  /** True from a call that found no server until the next call that got an answer. */
  isDown: () => boolean;
};

/**
 * Whether the server answers, from the app's own calls: every query and command of the client, through its caches. A store for
 * `useSyncExternalStore`, so the shell's connection bar follows it without a request of its own.
 */
export function serverReach(queryClient: QueryClient): ServerReach {
  let down = false;
  const listeners = new Set<() => void>();
  const set = (next: boolean) => {
    if (next === down) return;
    down = next;
    for (const listener of listeners) listener();
  };
  // An answer, a refusal too, says the server is up; any other failure, such as a body that does not parse, says nothing.
  const failed = (error: unknown) => {
    if (error instanceof ProblemDetailsError || error instanceof TypeError)
      set(isServerUnreachable(error));
  };
  let stop: (() => void) | undefined;
  const start = () => {
    const queries = queryClient.getQueryCache().subscribe((event) => {
      if (event.type !== "updated") return;
      if (event.action.type === "success") set(false);
      if (event.action.type === "error") failed(event.action.error);
    });
    const commands = queryClient.getMutationCache().subscribe((event) => {
      if (event.type !== "updated") return;
      if (event.action.type === "success") set(false);
      if (event.action.type === "error") failed(event.action.error);
    });
    stop = () => {
      queries();
      commands();
    };
  };
  return {
    subscribe: (changed) => {
      if (listeners.size === 0) start();
      listeners.add(changed);
      return () => {
        listeners.delete(changed);
        if (listeners.size === 0) stop?.();
      };
    },
    isDown: () => down,
  };
}
