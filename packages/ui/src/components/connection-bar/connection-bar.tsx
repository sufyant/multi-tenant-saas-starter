"use client";

import { useTranslations } from "next-intl";
import { useEffect, useReducer, useRef, useState } from "react";
import { useOnline } from "../../lib/use-online";
import { cn } from "../../lib/utils";

/** The waits between two tries (the Connection bar): 5, 10, then 30 seconds for every later try. */
export const retryDelays = [5, 10, 30] as const;
/** How long "Back online" stays. */
export const backOnlineMs = 2000;

export type ConnectionBarProps = {
  /** The server did not answer the app's last call: a network error, 502, 503 or 504 (`serverReach` of the client). */
  serverDown: boolean;
  /** Tries again: reads the page's queries and sends the waiting commands. */
  onRetry: () => void;
  /** The product's name: "Starter isn’t answering." */
  product: string;
  /** The brand's status page; without it the bar shows no link. */
  statusUrl?: string;
};

type Problem = "offline" | "server" | null;

type Countdown = { tries: number; left: number };
const start: Countdown = { tries: 0, left: 5 };
const waitAfter = (tries: number) =>
  retryDelays[Math.min(tries, retryDelays.length - 1)] ?? 30;

/** One second passes, the countdown ends in a try, "Try now" tries at once, or a new problem starts again. */
function next(
  countdown: Countdown,
  event: "tick" | "try" | "reset",
): Countdown {
  if (event === "reset") return start;
  if (event === "tick" && countdown.left > 1)
    return { ...countdown, left: countdown.left - 1 };
  const tries = countdown.tries + 1;
  return { tries, left: waitAfter(tries) };
}

/**
 * The bar of a connection problem (the Connection bar): offline or a server that does not answer, in amber, with a countdown to
 * the next try after 5, 10 and 30 seconds, and "Try now"; "Back online" in green once an answer comes. A connection problem comes
 * before every other bar. The text is a polite live region, so a screen reader hears each change once, not each second.
 */
export function ConnectionBar({
  serverDown,
  onRetry,
  product,
  statusUrl,
}: ConnectionBarProps) {
  const t = useTranslations("appShell.connection");
  const online = useOnline();
  const problem: Problem = !online ? "offline" : serverDown ? "server" : null;
  const [countdown, step] = useReducer(next, start);
  const [back, setBack] = useState(false);
  const had = useRef<Problem>(null);
  const retry = useRef(onRetry);
  retry.current = onRetry;

  // A new problem starts the countdown from its first wait; its end shows "Back online".
  useEffect(() => {
    if (problem && !had.current) step("reset");
    if (!problem && had.current) setBack(true);
    had.current = problem;
  }, [problem]);

  useEffect(() => {
    if (!back) return;
    const timer = setTimeout(() => setBack(false), backOnlineMs);
    return () => clearTimeout(timer);
  }, [back]);

  useEffect(() => {
    if (!problem) return;
    const timer = setInterval(() => step("tick"), 1000);
    return () => clearInterval(timer);
  }, [problem]);

  // Each try, by the countdown or by "Try now", calls the app once.
  useEffect(() => {
    if (countdown.tries > 0) retry.current();
  }, [countdown.tries]);

  if (!problem && !back) return null;
  const { left: seconds } = countdown;
  return (
    <div
      data-slot="connection-bar"
      data-state={problem ?? "back-online"}
      className={cn(
        "flex min-h-8 shrink-0 flex-wrap items-center justify-center gap-x-2 px-4 py-1 text-center text-small font-medium",
        problem
          ? "bg-warning-subtle text-warning-text"
          : "bg-success-subtle text-success-text",
      )}
    >
      <span role="status">
        {problem === "offline" && t("offline", { seconds })}
        {problem === "server" && t("server", { product, seconds })}
        {!problem && t("backOnline")}
      </span>
      {problem && (
        <>
          <span aria-hidden="true">·</span>
          <button
            type="button"
            onClick={() => step("try")}
            className="rounded-sm underline underline-offset-4 outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {t("tryNow")}
          </button>
        </>
      )}
      {problem === "server" && statusUrl && (
        <a
          href={statusUrl}
          target="_blank"
          rel="noreferrer"
          className="rounded-sm font-normal text-primary underline underline-offset-4 outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {t("statusPage")}
        </a>
      )}
    </div>
  );
}
