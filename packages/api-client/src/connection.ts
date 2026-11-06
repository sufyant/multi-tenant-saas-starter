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
  /** True from the second try that found no server until the next call that got an answer. */
  isDown: () => boolean;
};

/** The step of a query or a command that its cache reports. */
type CallAction = { type: string; error?: unknown };

/**
 * Whether the server answers, from the app's own calls: every query and command of the client, through its caches. A store for
 * `useSyncExternalStore`, so the shell's connection bar follows it without a request of its own. One try that finds no server
 * only raises a doubt, and the next try decides (the Connection bar). A try is a call that starts after the doubt: a retry, the
 * bar's try or a new call. Calls that fail together, as on a wake from sleep, are one try.
 */
export function serverReach(queryClient: QueryClient): ServerReach {
  let down = false;
  // Each start and each failure takes the next number, so a failure knows whether its call started after the doubt.
  let clock = 0;
  let doubtSince: number | undefined;
  const startedAt = new WeakMap<object, number>();
  const listeners = new Set<() => void>();
  const set = (next: boolean) => {
    if (next === down) return;
    down = next;
    for (const listener of listeners) listener();
  };
  const answered = () => {
    doubtSince = undefined;
    set(false);
  };
  const foundNoServer = (call: object) => {
    if (doubtSince === undefined) doubtSince = ++clock;
    else if ((startedAt.get(call) ?? 0) > doubtSince) set(true);
  };
  // An answer, a refusal too, says the server is up; a cancelled call or a body that does not parse says nothing.
  const failed = (call: object, error: unknown) => {
    if (isServerUnreachable(error)) foundNoServer(call);
    else if (error instanceof ProblemDetailsError) answered();
  };
  const follow = (call: object, action: CallAction) => {
    if (action.type === "fetch" || action.type === "pending")
      startedAt.set(call, ++clock);
    if (action.type === "success") answered();
    if (action.type === "failed" || action.type === "error")
      failed(call, action.error);
    // A retry starts after the failure that it follows.
    if (action.type === "failed") startedAt.set(call, ++clock);
  };
  let stop: (() => void) | undefined;
  const start = () => {
    const queries = queryClient.getQueryCache().subscribe((event) => {
      if (event.type === "updated") follow(event.query, event.action);
    });
    const commands = queryClient.getMutationCache().subscribe((event) => {
      if (event.type === "updated") follow(event.mutation, event.action);
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
