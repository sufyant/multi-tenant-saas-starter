"use client";

import { AppError } from "@repo/ui/components/app-error";
import { useAppFrame } from "@repo/ui/components/app-frame";
import { useEffect } from "react";
import { reportBrowserError } from "@/error-tracking/report-error";

type ErrorProps = { error: Error & { digest?: string }; retry: () => void };

/** Sends the error once to error tracking (F55). */
function useReport(error: Error) {
  useEffect(() => {
    console.error(error);
    reportBrowserError(error);
  }, [error]);
}

/** The error.tsx of a route inside the shell (F27): the error stays in the content, and the shell and its menu stay. */
export function SectionErrorPage({ error, retry }: ErrorProps) {
  useReport(error);
  return (
    <AppError kind="section" code={error.digest} onRetry={() => retry()} />
  );
}

/** The error.tsx of a route outside the shell (F27): the app could not start, the only full-page error. */
export function StartErrorPage({ error }: ErrorProps) {
  useReport(error);
  const { version } = useAppFrame();
  return (
    <AppError
      kind="start"
      code={error.digest}
      version={version}
      onRetry={() => window.location.reload()}
    />
  );
}
